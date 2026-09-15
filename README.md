# car-repair-auth-lambda

AWS Lambda em .NET 8 para autenticacao por CPF do sistema Car Repair.

Este repositorio gerencia somente a Lambda de autenticacao, sua IAM role, CloudWatch Logs, o secret JWT e API Gateway opcional. A VPC/EKS vem do `car-repair-k8s-infra` e o RDS/secret do banco/security group client vem do `car-repair-db-infra`.

## Proposito

Esta Lambda e o componente de autenticacao do sistema Car Repair. Ela recebe um CPF, valida o formato, consulta o PostgreSQL para localizar um cliente ativo e, quando o cliente e encontrado, emite um JWT assinado para consumo pelos demais componentes da solucao.

## Escopo

Esta stack cria:

- AWS Lambda .NET 8
- IAM role de execucao da Lambda
- permissoes de CloudWatch Logs
- permissoes de Secrets Manager somente para os secrets configurados
- permissao gerenciada `AWSLambdaVPCAccessExecutionRole` para ENIs em VPC
- secret JWT em Secrets Manager
- API Gateway opcional

Esta stack nao cria:

- VPC
- EKS
- RDS
- Security Group novo para banco
- RDS Proxy
- Kong
- New Relic

## Fluxo de autenticacao

```text
Cliente
  |
  | envia CPF
  v
AWS Lambda de Autenticacao
  |
  | valida CPF
  | consulta PostgreSQL
  | le secrets no AWS Secrets Manager
  | gera JWT
  v
Resposta com token JWT
```

1. O cliente envia uma requisicao contendo o campo `cpf`.
2. A Lambda normaliza e valida o CPF informado.
3. A Lambda consulta o PostgreSQL para buscar um cliente com esse CPF.
4. Se o cliente nao for encontrado, a Lambda retorna erro de negocio.
5. Se o cliente estiver inativo, a Lambda retorna erro de negocio.
6. Se o cliente estiver ativo, a Lambda obtem no AWS Secrets Manager:
   - o secret de conexao com o banco
   - o secret com a chave de assinatura do JWT
7. A Lambda gera um token JWT com os dados do cliente autenticado.
8. A Lambda retorna a resposta com o `accessToken`, data de expiracao, identificador e nome do cliente.

Secrets consumidos:

```text
Lambda
 |
 v
Secrets Manager
 |-- car-repair/<environment>/database
 `-- car-repair/<environment>/jwt
```

## Integracao com os repositorios de infra

Do `car-repair-k8s-infra`:

- `private_subnets` -> `private_subnet_ids`

Do `car-repair-db-infra`:

- `database_client_security_group_id` -> `database_client_security_group_id`
- `database_secret_arn` -> `database_secret_arn`
- `database_secret_name` -> `database_secret_name`

A Lambda usa apenas subnets privadas:

```hcl
vpc_config {
  subnet_ids         = var.private_subnet_ids
  security_group_ids = [var.database_client_security_group_id]
}
```

O Security Group reutilizado ja e autorizado pelo RDS para PostgreSQL `5432/tcp`.

## Secrets Manager

### Database

O secret do banco pertence ao `car-repair-db-infra` e deve seguir:

```text
car-repair/<environment>/database
```

Exemplos:

- `car-repair/dev/database`
- `car-repair/prod/database`

Este repositorio nao cria o secret do banco. A IAM policy da Lambda permite leitura somente do secret informado por `database_secret_arn` ou, se o ARN nao for informado, do secret resolvido por `database_secret_name`.

### JWT

Este repositorio cria e gerencia o secret JWT:

```text
car-repair/<environment>/jwt
```

Exemplos:

- `car-repair/dev/jwt`
- `car-repair/prod/jwt`

A chave JWT e gerada automaticamente e armazenada no Secrets Manager. Ela nao deve ser colocada em:

- `terraform.tfvars`
- environment variables
- outputs
- codigo
- GitHub variables

## Acesso ao Secrets Manager em private subnet

A Lambda fica em private subnets. Para buscar os secrets e conectar ao RDS privado, ela depende de:

- rota das private subnets para NAT Gateway existente no `car-repair-k8s-infra`, para chamadas ao Secrets Manager
- rota interna da VPC para o RDS
- `database_client_security_group_id` autorizado no Security Group do RDS

Nao foi criado VPC Endpoint para Secrets Manager nesta etapa. Se NAT Gateway for removido ou bloqueado, a alternativa operacional e criar um Interface VPC Endpoint para `secretsmanager`.

## Timeout e conexoes

O timeout padrao da Lambda e `20` segundos. Esse valor considera cold start, criacao de ENI, chamada ao Secrets Manager e conexao PostgreSQL sem exagerar o tempo maximo de execucao.

No codigo:

- o secret de banco e cacheado em memoria por container Lambda
- o Npgsql usa pooling
- o EF Core usa `AddDbContextPool`
- nao ha RDS Proxy nesta etapa

## Variaveis Terraform principais

- `private_subnet_ids`
- `database_client_security_group_id`
- `database_secret_arn`
- `database_secret_name`
- `jwt_secret_name`
- `lambda_timeout`
- `enable_api_gateway`

Exemplos:

- `terraform/environments/dev.tfvars.example`
- `terraform/environments/hml.tfvars.example`
- `terraform/environments/prod.tfvars.example`

## Tecnologias

- AWS Lambda
- .NET 8 / C#
- PostgreSQL
- Entity Framework Core
- Npgsql
- AWS Secrets Manager
- Terraform
- GitHub Actions

## Build local

```bash
dotnet restore CarRepair.Auth.Lambda.sln
dotnet build CarRepair.Auth.Lambda.sln
dotnet test CarRepair.Auth.Lambda.sln
```

## Terraform

Validacao:

```bash
terraform -chdir=terraform init
terraform -chdir=terraform validate
```

Plano com exemplo dev:

```bash
terraform -chdir=terraform plan -var-file=environments/dev.tfvars
```

## Outputs

- `lambda_function_name`
- `lambda_function_arn`
- `jwt_secret_arn`
- `jwt_secret_name`
- `api_gateway_invoke_url`, quando `enable_api_gateway = true`

Nenhum output expoe valor do secret JWT ou credenciais do banco.

## Deploy

O workflow `.github/workflows/cd.yml` publica a Lambda e executa Terraform para os ambientes `hml` e `prod`. Configure as variables abaixo:

- `AWS_REGION`
- `PRIVATE_SUBNET_IDS_JSON`, exemplo: `["subnet-aaa","subnet-bbb"]`
- `DATABASE_CLIENT_SECURITY_GROUP_ID`
- `DATABASE_SECRET_ARN`
- `DATABASE_SECRET_NAME`
- `JWT_SECRET_NAME`
- `JWT_ISSUER`
- `JWT_AUDIENCE`
- `JWT_EXPIRATION_IN_MINUTES`
- `DB_SCHEMA`
- `CUSTOMERS_TABLE_NAME`
- `ENABLE_API_GATEWAY`

Configure o secret GitHub:

- `AWS_ROLE_TO_ASSUME`

## Relacionamento com os demais repositorios

| Repositorio | Responsabilidade |
|------------|------------------|
| car-repair-app | API principal |
| car-repair-auth-lambda | Emissao de JWT |
| car-repair-db-infra | Banco PostgreSQL |
| car-repair-k8s-infra | Plataforma Kubernetes |
