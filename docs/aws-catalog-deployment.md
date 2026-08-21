# Implantacao AWS do catalogo

Execute API e CatalogSync Worker como workloads separados. A API le o catalogo
exclusivamente do PostgreSQL; sincronizacao, acesso a Carzin e cleanup permanecem
no host dedicado. Uma topologia recomendada usa ALB + ECS/Fargate para a API,
ECS/Fargate ou tarefa agendada para o Worker, RDS/Aurora PostgreSQL com backups e
Multi-AZ conforme o requisito de disponibilidade, Secrets Manager para strings e
tokens e CloudWatch para logs JSON e metricas OpenTelemetry/.NET.

O SeedSync de fabricantes/modelos/versoes e as migrations devem ser executados
antes da primeira sincronizacao. O Worker resolve os codigos da Carzin usando os
catalogos persistidos e grava somente nomes no catalogo publicado; a API nao tem
cliente, fallback ou endpoint de detalhes da Carzin.

O ALB deve usar `/health/live` para processo. Orquestradores e gates de trafego
devem consultar `/health/ready`. Readiness avalia publicacao, ponteiros, contagens,
idade e relogio; uma publicacao stale torna readiness unhealthy, mas buscas GET
continuam servindo o snapshot publicado para evitar uma interrupcao autoinduzida.

Restrinja security groups do banco aos tasks, use TLS, usuario com privilegios
minimos e migration job separado. Nao grave `DATABASE_URL`, tokens, filtros,
IDs externos em logs. Alarmes devem observar os instrumentos `Buscarro.Catalog`,
falhas/timeout, idade da publicacao, latencia e readiness, com limites definidos
somente apos baseline medido.

Rollback de dados e operacao manual one-shot no Worker:

```text
rollback --provider Carzin --target-generation 41
```

Ele adquire o mesmo lock do sync, aceita apenas a geracao `Previous` apontada e
publica uma nova versao global atomicamente. Repetir o mesmo alvo falha com
conflito; nao ha alternancia automatica. Rollback de aplicacao permanece a cargo
do deployment da API.
