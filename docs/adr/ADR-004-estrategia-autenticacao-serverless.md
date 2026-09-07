# ADR-004 - Estratégia de Autenticação Serverless

- **Status:** Aceito
- **Data:** 2026-09-07

## Contexto

O serviço `car-repair-auth-lambda` autentica clientes por CPF e emite JWT para consumo das APIs do ecossistema Car Repair.
O roadmap inclui integração com Kong Gateway (edge), EKS (workloads de negócio), AWS Secrets Manager (segredos centralizados) e New Relic (observabilidade).

Historicamente, a chave JWT era injetada por variável Terraform sensível e o secret PostgreSQL era criado neste repositório, duplicando responsabilidade já coberta pelo `car-repair-db-infra`.

## Decisão

1. A chave JWT passa a ser armazenada em um secret dedicado no AWS Secrets Manager, gerenciado por este stack.
2. A Lambda passa a ler em runtime:
   - secret de conexão PostgreSQL externo (`car-repair-db-infra`)
   - secret JWT dedicado deste stack
3. O API Gateway deste repositório torna-se opcional via `enable_api_gateway`, para permitir operação com Kong como camada de autenticação e roteamento em produção.
4. O JWT emitido inclui claim de autorização estática `role=Customer`.

## Consequências

### Positivas

- Reduz exposição de segredo JWT em variáveis de pipeline/Terraform.
- Elimina duplicação de secret de banco entre repositórios.
- Permite transição gradual para arquitetura com Kong sem quebrar ambientes atuais.
- Mantém princípio de menor privilégio no IAM para acesso a logs e segredos necessários.

### Trade-offs

- Dependência explícita de dois secrets no Secrets Manager para runtime da Lambda.
- Chave JWT gerada automaticamente exige estratégia futura de rotação controlada e distribuição de chaves públicas/privadas, se aplicável.

## Fluxo arquitetural alvo

`CPF → Lambda → JWT → Kong → API`
