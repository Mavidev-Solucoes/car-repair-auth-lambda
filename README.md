# car-repair-auth-lambda

Esse projeto implementa uma AWS Lambda em .NET 8 para autenticação via CPF do sistema Car Repair Shop.

## Arquitetura

- `src/Domain`: entidades e validação de CPF
- `src/Application`: caso de uso de autenticação, contratos e regras de validação
- `src/Infrastructure`: Entity Framework Core, PostgreSQL, JWT e integração com AWS Secrets Manager
- `src/Lambda`: handler da AWS Lambda, logging estruturado e correlation ID
- `terraform`: infraestrutura completa da Lambda, API Gateway, IAM, CloudWatch Logs e Secrets Manager

## Fluxo

1. Recebe o CPF em `POST /auth/token`
2. Valida o CPF com FluentValidation e algoritmo oficial
3. Busca o cliente no PostgreSQL
4. Confirma que o cliente existe e está ativo
5. Gera o JWT
6. Retorna o token com `CorrelationId` no header `X-Correlation-Id`

## Payload de entrada

```json
{
  "cpf": "12345678909"
}
```

## Resposta de sucesso

```json
{
  "accessToken": "jwt",
  "expiresAtUtc": "2026-01-01T00:00:00Z",
  "customerId": "00000000-0000-0000-0000-000000000000",
  "customerName": "Maria Souza",
  "tokenType": "Bearer"
}
```

## Configuração

A Lambda espera as seguintes configurações:

- `SecretsManager__ConnectionStringSecretId`: nome ou ARN do secret com a connection string do PostgreSQL
- `Jwt__SecretKey`: chave de assinatura do JWT
- `Jwt__Issuer`: issuer do token
- `Jwt__Audience`: audience do token
- `Jwt__ExpirationInMinutes`: expiração em minutos
- `Database__Schema`: schema do PostgreSQL
- `Database__CustomersTableName`: tabela de clientes

O secret do banco pode ser armazenado em um dos formatos abaixo:

```json
{
  "connectionString": "Host=...;Port=5432;Database=...;Username=...;******"
}
```

ou

```json
{
  "host": "db.example.com",
  "port": 5432,
  "database": "car_repair",
  "username": "app",
  "password": "secret"
}
```

## Build local

```bash
dotnet restore /home/runner/work/car-repair-auth-lambda/car-repair-auth-lambda/CarRepair.Auth.Lambda.sln
dotnet build /home/runner/work/car-repair-auth-lambda/car-repair-auth-lambda/CarRepair.Auth.Lambda.sln
```

## Deploy

O workflow `.github/workflows/deploy.yml` publica a Lambda e executa o Terraform. Configure os secrets e variables do GitHub abaixo antes de usar:

- `AWS_ROLE_TO_ASSUME`
- `POSTGRES_CONNECTION_STRING`
- `JWT_SECRET_KEY`
- `AWS_REGION` (variable)
- `ENVIRONMENT` (variable)
- `POSTGRES_SECRET_NAME` (variable, opcional)
- `DB_SCHEMA` (variable, opcional)
- `CUSTOMERS_TABLE_NAME` (variable, opcional)
