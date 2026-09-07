output "lambda_function_name" {
  description = "Name of the deployed Lambda function."
  value       = aws_lambda_function.auth.function_name
}

output "api_gateway_invoke_url" {
  description = "Invoke URL for the deployed API Gateway stage."
  value       = aws_api_gateway_stage.auth.invoke_url
}

output "postgres_secret_arn" {
  description = "ARN of the Secrets Manager secret that stores the PostgreSQL connection string."
  value       = aws_secretsmanager_secret.postgres_connection.arn
  sensitive   = true
}
