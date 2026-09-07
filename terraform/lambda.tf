terraform {
  required_version = ">= 1.6.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

resource "aws_secretsmanager_secret" "postgres_connection" {
  name                    = var.postgres_secret_name
  recovery_window_in_days = 0
  tags                    = local.common_tags
}

resource "aws_secretsmanager_secret_version" "postgres_connection" {
  secret_id     = aws_secretsmanager_secret.postgres_connection.id
  secret_string = jsonencode({ connectionString = var.postgres_connection_string })
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
      Jwt__SecretKey                           = var.jwt_secret_key
      SecretsManager__ConnectionStringSecretId = aws_secretsmanager_secret.postgres_connection.name
      Database__Schema                         = var.db_schema
      Database__CustomersTableName             = var.customers_table_name
    }
  }

  depends_on = [
    aws_cloudwatch_log_group.lambda,
    aws_iam_role_policy_attachment.basic_execution,
    aws_iam_role_policy.secrets_access,
    aws_secretsmanager_secret_version.postgres_connection
  ]

  tags = local.common_tags
}

resource "aws_api_gateway_rest_api" "auth" {
  name        = "${local.resource_prefix}-api"
  description = "API Gateway for the Car Repair auth Lambda."
  endpoint_configuration {
    types = ["REGIONAL"]
  }

  tags = local.common_tags
}

resource "aws_api_gateway_resource" "auth" {
  rest_api_id = aws_api_gateway_rest_api.auth.id
  parent_id   = aws_api_gateway_rest_api.auth.root_resource_id
  path_part   = "auth"
}

resource "aws_api_gateway_resource" "token" {
  rest_api_id = aws_api_gateway_rest_api.auth.id
  parent_id   = aws_api_gateway_resource.auth.id
  path_part   = "token"
}

resource "aws_api_gateway_method" "post_token" {
  rest_api_id   = aws_api_gateway_rest_api.auth.id
  resource_id   = aws_api_gateway_resource.token.id
  http_method   = "POST"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "post_token" {
  rest_api_id             = aws_api_gateway_rest_api.auth.id
  resource_id             = aws_api_gateway_resource.token.id
  http_method             = aws_api_gateway_method.post_token.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = aws_lambda_function.auth.invoke_arn
}

resource "aws_api_gateway_method" "options_token" {
  rest_api_id   = aws_api_gateway_rest_api.auth.id
  resource_id   = aws_api_gateway_resource.token.id
  http_method   = "OPTIONS"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "options_token" {
  rest_api_id = aws_api_gateway_rest_api.auth.id
  resource_id = aws_api_gateway_resource.token.id
  http_method = aws_api_gateway_method.options_token.http_method
  type        = "MOCK"

  request_templates = {
    "application/json" = "{\"statusCode\": 200}"
  }
}

resource "aws_api_gateway_method_response" "options_token" {
  rest_api_id = aws_api_gateway_rest_api.auth.id
  resource_id = aws_api_gateway_resource.token.id
  http_method = aws_api_gateway_method.options_token.http_method
  status_code = "200"

  response_parameters = {
    "method.response.header.Access-Control-Allow-Headers" = true
    "method.response.header.Access-Control-Allow-Methods" = true
    "method.response.header.Access-Control-Allow-Origin"  = true
  }
}

resource "aws_api_gateway_integration_response" "options_token" {
  rest_api_id = aws_api_gateway_rest_api.auth.id
  resource_id = aws_api_gateway_resource.token.id
  http_method = aws_api_gateway_method.options_token.http_method
  status_code = aws_api_gateway_method_response.options_token.status_code

  response_parameters = {
    "method.response.header.Access-Control-Allow-Headers" = "'Content-Type,X-Correlation-Id'"
    "method.response.header.Access-Control-Allow-Methods" = "'OPTIONS,POST'"
    "method.response.header.Access-Control-Allow-Origin"  = "'*'"
  }
}

resource "aws_api_gateway_deployment" "auth" {
  rest_api_id = aws_api_gateway_rest_api.auth.id

  triggers = {
    redeployment = sha1(jsonencode([
      aws_api_gateway_resource.auth.id,
      aws_api_gateway_resource.token.id,
      aws_api_gateway_method.post_token.id,
      aws_api_gateway_integration.post_token.id,
      aws_api_gateway_method.options_token.id,
      aws_api_gateway_integration.options_token.id,
      aws_api_gateway_method_response.options_token.id,
      aws_api_gateway_integration_response.options_token.id,
      aws_lambda_function.auth.source_code_hash
    ]))
  }

  lifecycle {
    create_before_destroy = true
  }

  depends_on = [
    aws_api_gateway_integration.post_token,
    aws_api_gateway_integration.options_token,
    aws_api_gateway_integration_response.options_token
  ]
}

resource "aws_api_gateway_stage" "auth" {
  rest_api_id   = aws_api_gateway_rest_api.auth.id
  deployment_id = aws_api_gateway_deployment.auth.id
  stage_name    = var.environment
  tags          = local.common_tags
}

resource "aws_lambda_permission" "api_gateway" {
  statement_id  = "AllowExecutionFromApiGateway"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.auth.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.auth.execution_arn}/*/*"
}
