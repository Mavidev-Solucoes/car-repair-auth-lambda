locals {
  resource_prefix = "${var.project_name}-${var.environment}"

  database_secret_arn = trimspace(var.database_secret_arn) != "" ? var.database_secret_arn : data.aws_secretsmanager_secret.database[0].arn

  database_secret_runtime_id = trimspace(var.database_secret_name) != "" ? var.database_secret_name : (
    trimspace(var.database_secret_arn) != "" ? var.database_secret_arn : "car-repair/${var.environment}/database"
  )

  jwt_secret_name = trimspace(var.jwt_secret_name) != "" ? var.jwt_secret_name : "car-repair/${var.environment}/jwt"

  lambda_execution_role_arn = var.create_execution_role ? aws_iam_role.lambda_execution[0].arn : var.execution_role_arn

  common_tags = merge({
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }, var.tags)
}

resource "aws_iam_role" "lambda_execution" {
  count = var.create_execution_role ? 1 : 0

  name = "${local.resource_prefix}-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"

    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"

        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_iam_role_policy_attachment" "lambda_vpc_access" {
  count = var.create_execution_role ? 1 : 0

  role       = aws_iam_role.lambda_execution[0].name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

resource "aws_iam_role_policy" "lambda_runtime" {
  count = var.create_execution_role ? 1 : 0

  name = "${local.resource_prefix}-runtime-policy"
  role = aws_iam_role.lambda_execution[0].id

  policy = jsonencode({
    Version = "2012-10-17"

    Statement = [
      {
        Effect = "Allow"

        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]

        Resource = [
          aws_cloudwatch_log_group.lambda.arn,
          "${aws_cloudwatch_log_group.lambda.arn}:*"
        ]
      },
      {
        Effect = "Allow"

        Action = [
          "secretsmanager:GetSecretValue",
          "secretsmanager:DescribeSecret"
        ]

        Resource = [
          local.database_secret_arn,
          aws_secretsmanager_secret.jwt_signing_key.arn
        ]
      }
    ]
  })
}