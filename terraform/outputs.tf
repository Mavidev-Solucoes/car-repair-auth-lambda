output "lambda_function_name" {
  description = "Name of the deployed Lambda function."
  value       = aws_lambda_function.auth.function_name
}

output "lambda_function_arn" {
  description = "ARN of the deployed Lambda function."
  value       = aws_lambda_function.auth.arn
}

output "api_gateway_invoke_url" {
  description = "Invoke URL for the deployed API Gateway stage."
  value       = var.enable_api_gateway ? aws_api_gateway_stage.auth[0].invoke_url : null
}

output "jwt_secret_arn" {
  description = "ARN of the Secrets Manager secret that stores the JWT signing key."
  value       = aws_secretsmanager_secret.jwt_signing_key.arn
}

output "jwt_secret_name" {
  description = "Name of the Secrets Manager secret that stores the JWT signing key."
  value       = aws_secretsmanager_secret.jwt_signing_key.name
}
