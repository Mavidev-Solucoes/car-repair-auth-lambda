terraform {
  required_version = ">= 1.6.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

resource "aws_secretsmanager_secret" "jwt_signing_key" {
  name                    = var.jwt_secret_name
  recovery_window_in_days = 0
  tags                    = local.common_tags
}

resource "random_password" "jwt_signing_key" {
  length  = 64
  special = true
}

resource "aws_secretsmanager_secret_version" "jwt_signing_key" {
  secret_id     = aws_secretsmanager_secret.jwt_signing_key.id
  secret_string = jsonencode({ secretKey = random_password.jwt_signing_key.result })
}

data "aws_secretsmanager_secret" "postgres_external" {
  count = startswith(var.postgres_secret_id, "arn:") ? 0 : 1
  name  = var.postgres_secret_id
}

locals {
  postgres_secret_arn = startswith(var.postgres_secret_id, "arn:") ? var.postgres_secret_id : data.aws_secretsmanager_secret.postgres_external[0].arn
}

resource "aws_cloudwatch_log_group" "lambda" {
  name              = "/aws/lambda/${local.resource_prefix}"
  retention_in_days = 14
  tags              = local.common_tags
}

resource "aws_lambda_function" "auth" {
  function_name    = local.resource_prefix
  description      = "Lambda responsible for CPF-based authentication for Car Repair Shop."
  role             = aws_iam_role.lambda_execution.arn
  runtime          = "dotnet8"
  handler          = var.lambda_handler
  filename         = var.lambda_package_path
  source_code_hash = filebase64sha256(var.lambda_package_path)
  memory_size      = var.lambda_memory_size
  timeout          = var.lambda_timeout

  environment {
    variables = {
      ASPNETCORE_ENVIRONMENT                   = var.environment
      Jwt__Issuer                              = var.jwt_issuer
      Jwt__Audience                            = var.jwt_audience
      Jwt__ExpirationInMinutes                 = tostring(var.jwt_expiration_in_minutes)
      SecretsManager__ConnectionStringSecretId = var.postgres_secret_id
      SecretsManager__JwtSecretId              = aws_secretsmanager_secret.jwt_signing_key.name
      Database__Schema                         = var.db_schema
      Database__CustomersTableName             = var.customers_table_name
    }
  }

  depends_on = [
    aws_cloudwatch_log_group.lambda,
    aws_iam_role_policy.lambda_runtime,
    aws_secretsmanager_secret_version.jwt_signing_key
  ]

  tags = local.common_tags
}

resource "aws_api_gateway_rest_api" "auth" {
  count       = var.enable_api_gateway ? 1 : 0
  name        = "${local.resource_prefix}-api"
  description = "API Gateway for the Car Repair auth Lambda."
  endpoint_configuration {
    types = ["REGIONAL"]
  }

  tags = local.common_tags
}

resource "aws_api_gateway_resource" "auth" {
  count       = var.enable_api_gateway ? 1 : 0
  rest_api_id = aws_api_gateway_rest_api.auth[0].id
  parent_id   = aws_api_gateway_rest_api.auth[0].root_resource_id
  path_part   = "auth"
}

resource "aws_api_gateway_resource" "token" {
  count       = var.enable_api_gateway ? 1 : 0
  rest_api_id = aws_api_gateway_rest_api.auth[0].id
  parent_id   = aws_api_gateway_resource.auth[0].id
  path_part   = "token"
}

resource "aws_api_gateway_method" "post_token" {
  count         = var.enable_api_gateway ? 1 : 0
  rest_api_id   = aws_api_gateway_rest_api.auth[0].id
  resource_id   = aws_api_gateway_resource.token[0].id
  http_method   = "POST"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "post_token" {
  count                   = var.enable_api_gateway ? 1 : 0
  rest_api_id             = aws_api_gateway_rest_api.auth[0].id
  resource_id             = aws_api_gateway_resource.token[0].id
  http_method             = aws_api_gateway_method.post_token[0].http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = aws_lambda_function.auth.invoke_arn
}

resource "aws_api_gateway_method" "options_token" {
  count         = var.enable_api_gateway ? 1 : 0
  rest_api_id   = aws_api_gateway_rest_api.auth[0].id
  resource_id   = aws_api_gateway_resource.token[0].id
  http_method   = "OPTIONS"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "options_token" {
  count       = var.enable_api_gateway ? 1 : 0
  rest_api_id = aws_api_gateway_rest_api.auth[0].id
  resource_id = aws_api_gateway_resource.token[0].id
  http_method = aws_api_gateway_method.options_token[0].http_method
  type        = "MOCK"

  request_templates = {
    "application/json" = "{\"statusCode\": 200}"
  }
}

resource "aws_api_gateway_method_response" "options_token" {
  count       = var.enable_api_gateway ? 1 : 0
  rest_api_id = aws_api_gateway_rest_api.auth[0].id
  resource_id = aws_api_gateway_resource.token[0].id
  http_method = aws_api_gateway_method.options_token[0].http_method
  status_code = "200"

  response_parameters = {
    "method.response.header.Access-Control-Allow-Headers" = true
    "method.response.header.Access-Control-Allow-Methods" = true
    "method.response.header.Access-Control-Allow-Origin"  = true
  }
}

resource "aws_api_gateway_integration_response" "options_token" {
  count       = var.enable_api_gateway ? 1 : 0
  rest_api_id = aws_api_gateway_rest_api.auth[0].id
  resource_id = aws_api_gateway_resource.token[0].id
  http_method = aws_api_gateway_method.options_token[0].http_method
  status_code = aws_api_gateway_method_response.options_token[0].status_code

  response_parameters = {
    "method.response.header.Access-Control-Allow-Headers" = "'Content-Type,X-Correlation-Id'"
    "method.response.header.Access-Control-Allow-Methods" = "'OPTIONS,POST'"
    "method.response.header.Access-Control-Allow-Origin"  = "'*'"
  }
}

resource "aws_api_gateway_deployment" "auth" {
  count       = var.enable_api_gateway ? 1 : 0
  rest_api_id = aws_api_gateway_rest_api.auth[0].id

  triggers = {
    redeployment = sha1(jsonencode([
      aws_api_gateway_resource.auth[0].id,
      aws_api_gateway_resource.token[0].id,
      aws_api_gateway_method.post_token[0].id,
      aws_api_gateway_integration.post_token[0].id,
      aws_api_gateway_method.options_token[0].id,
      aws_api_gateway_integration.options_token[0].id,
      aws_api_gateway_method_response.options_token[0].id,
      aws_api_gateway_integration_response.options_token[0].id,
      aws_lambda_function.auth.source_code_hash
    ]))
  }

  lifecycle {
    create_before_destroy = true
  }

  depends_on = [
    aws_api_gateway_integration.post_token[0],
    aws_api_gateway_integration.options_token[0],
    aws_api_gateway_integration_response.options_token[0]
  ]
}

resource "aws_api_gateway_stage" "auth" {
  count         = var.enable_api_gateway ? 1 : 0
  rest_api_id   = aws_api_gateway_rest_api.auth[0].id
  deployment_id = aws_api_gateway_deployment.auth[0].id
  stage_name    = var.environment
  tags          = local.common_tags
}

resource "aws_lambda_permission" "api_gateway" {
  count         = var.enable_api_gateway ? 1 : 0
  statement_id  = "AllowExecutionFromApiGateway"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.auth.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.auth[0].execution_arn}/*/*"
}
