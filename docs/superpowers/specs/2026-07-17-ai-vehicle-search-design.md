# Busca de veículos por linguagem natural — Design

**Data:** 2026-07-17

**Status:** aprovado para planejamento

**Escopo:** interpretação de prompts em critérios de busca; não inclui ranking semântico

## 1. Objetivo

Adicionar uma busca em linguagem natural ao Buscarro. O usuário informa um prompt como:

> Quero um carro seminovo para família, automático, até 100 mil reais em São Paulo.

O sistema interpreta o texto, produz uma intenção canônica estruturada, aplica regras de produto versionadas, resolve os critérios suportados e consulta o fluxo existente de veículos e providers.

O resultado deve conter:

- critérios explícitos identificados no prompt;
- critérios derivados de perfis semânticos;
- suposições e regras aplicadas;
- perfis reconhecidos mas não aplicáveis;
- veículos normalizados;
- status individual dos providers.

## 2. Não objetivos

O MVP não inclui:

- ordenação por relevância semântica;
- recomendação personalizada;
- RAG, embeddings ou banco vetorial;
- fine-tuning;
- memória conversacional;
- agente autônomo;
- acesso do modelo a HTTP, SQL ou providers;
- geração de URLs pelo modelo;
- inferência de consumo, segurança ou custo de manutenção sem dados confiáveis.

## 3. Decisões principais

### 3.1 Endpoint único

Será criado um endpoint orquestrador:

```http
POST /api/vehicles/ai-search
```

O backend não deve chamar seu próprio `GET /api/vehicles` por HTTP. O Handler reutilizará `IVehicleCatalogReadService`, o mesmo serviço utilizado pelo fluxo atual. Se o mapeamento de `Vehicle` para `VehicleResponse` ficar duplicado, ele deverá ser extraído para um mapper compartilhado na Application.

O `GET /api/vehicles` permanece disponível para filtros manuais e para buscas posteriores após o usuário editar os critérios interpretados.

### 3.2 Uma chamada ao modelo

O modelo terá uma única responsabilidade: extrair intenção explícita e classificar perfis semânticos. Ele não consultará veículos nem produzirá critérios específicos de provider.

### 3.3 Regras subjetivas ficam no backend

Expressões como “para família”, “seminovo” e “para iniciante” serão classificadas pelo modelo em códigos fechados. A transformação desses códigos em filtros será realizada por regras determinísticas, versionadas e testáveis no backend.

Structured Outputs controla o formato do JSON. O prompt de sistema e os exemplos orientam a classificação. As regras da aplicação controlam os filtros efetivamente aplicados. A aderência ao schema não garante que a interpretação semântica esteja correta; por isso, a qualidade será medida por evals.

## 4. Fluxo

```text
Frontend
  → POST /api/vehicles/ai-search
  → Controller
  → SearchVehiclesByAiQuery
  → SearchVehiclesByAiHandler
      → IVehicleSearchIntentInterpreter
      → validação da intenção
      → IVehicleSemanticProfileResolver
      → resolução de catálogo
      → VehicleSearchCriteria
      → IVehicleCatalogReadService
  → critérios + interpretação + veículos + status dos providers
```

O `CancellationToken` deve ser preservado em todas as etapas.

## 5. Controle da saída do modelo

### 5.1 JSON Schema estrito

O modelo deverá responder de acordo com um JSON Schema com estas propriedades:

- todos os campos definidos como obrigatórios;
- campos não informados representados por `null`;
- `additionalProperties: false` em todos os objetos;
- enums fechados para valores categóricos;
- `semanticProfiles` limitado a códigos conhecidos;
- nenhuma propriedade para IDs, URLs, SQL, providers ou veículos.

Exemplo conceitual da saída do modelo:

```json
{
  "explicitCriteria": {
    "query": null,
    "brand": null,
    "model": null,
    "version": null,
    "transmission": "automatic",
    "fuel": null,
    "color": null,
    "city": "São Paulo",
    "state": "SP",
    "priceMin": null,
    "priceMax": 100000,
    "yearMin": null,
    "yearMax": null,
    "order": null
  },
  "semanticProfiles": ["family", "semi_new"],
  "unresolvedTerms": []
}
```

Os códigos iniciais permitidos em `semanticProfiles` serão:

- `beginner`;
- `family`;
- `semi_new`;
- `economic`.

Novos códigos exigem nova versão do schema, regras, exemplos e evals.

### 5.2 Prompt e exemplos

O prompt de sistema deverá:

- extrair apenas informações afirmadas pelo usuário;
- usar `null` quando um critério não for informado;
- reconhecer paráfrases de perfis conhecidos;
- não definir limites numéricos para termos subjetivos;
- não inventar IDs ou entidades de catálogo;
- registrar termos sem suporte em `unresolvedTerms`;
- preservar contradições para validação da aplicação.

Exemplos few-shot devem ensinar classificação, não política de produto:

```text
“meu primeiro carro” → semanticProfiles: ["beginner"]
“um carro para levar as crianças” → semanticProfiles: ["family"]
“quero um seminovo” → semanticProfiles: ["semi_new"]
“quero algo que gaste pouco” → semanticProfiles: ["economic"]
```

## 6. Perfis semânticos

### 6.1 Contrato das regras

Cada perfil terá:

- código estável;
- `rulesVersion`;
- estado habilitado ou desabilitado;
- critérios derivados configurados;
- lista de capacidades/dados necessários;
- warning para aplicação parcial ou impossibilidade de aplicação.

Regras de precedência:

1. Restrições e filtros explícitos do usuário vencem por campo.
2. Perfis semânticos apenas preenchem campos ausentes.
3. Defaults técnicos são aplicados por último.
4. Contradições explícitas não são corrigidas silenciosamente.
5. Um perfil sem suporte deve ser devolvido como ignorado ou parcialmente aplicado.

### 6.2 Perfil `semi_new`

O modelo apenas classifica o perfil. O backend calcula:

```text
YearMin = TimeProvider.GetUtcNow().Year - SemiNewMaxAgeYears
```

`SemiNewMaxAgeYears` é uma configuração obrigatória para habilitar o perfil. O serviço não deve iniciar com o perfil habilitado se a configuração for inválida. Um `yearMin` explícito do usuário vence o valor derivado.

### 6.3 Perfil `family`

O sistema atual não possui critérios confiáveis de lugares, categoria de carroceria, volume do porta-malas ou segurança. Portanto, no MVP o modelo pode reconhecer `family`, mas o backend deve devolvê-lo em `ignoredProfiles` com o código `FAMILY_PROFILE_UNSUPPORTED`.

O perfil só poderá ser habilitado quando existirem dados e filtros confiáveis. Não será permitido traduzir automaticamente `family` para SUV ou para marcas/modelos específicos.

### 6.4 Perfil `beginner`

O perfil pode preencher apenas critérios configurados e já suportados, como `priceMax` e transmissão. Os valores devem ser definidos pelo produto em configuração versionada. Se o usuário solicitar transmissão automática, essa escolha vence um default manual do perfil.

### 6.5 Perfil `economic`

`Fuel` não representa consumo. Sem km/l, custo de manutenção ou outro indicador confiável, o perfil será reconhecido mas devolvido como parcial ou ignorado com `ECONOMIC_PROFILE_UNSUPPORTED`. O sistema não afirmará que um veículo é econômico com base apenas no combustível.

## 7. Contrato HTTP

### 7.1 Requisição

```json
{
  "prompt": "Quero um carro seminovo para família, automático, até 100 mil em SP",
  "page": 1,
  "results": 20,
  "locale": "pt-BR"
}
```

Validações:

- prompt obrigatório, normalizado e limitado;
- `page >= 1`;
- `results` dentro do limite configurado;
- locale permitido por allowlist;
- intervalos coerentes;
- nenhuma contradição explícita irreconciliável.

### 7.2 Resposta

```json
{
  "status": "completed",
  "interpretation": {
    "explicitCriteria": {
      "transmission": "automatic",
      "priceMax": 100000,
      "state": "SP"
    },
    "derivedCriteria": {
      "yearMin": 2023
    },
    "appliedProfiles": [
      {
        "code": "semi_new",
        "rulesVersion": "2026-07-17"
      }
    ],
    "ignoredProfiles": [
      {
        "code": "family",
        "reason": "FAMILY_PROFILE_UNSUPPORTED"
      }
    ],
    "unresolvedTerms": [],
    "warnings": []
  },
  "criteria": {
    "transmission": "automatic",
    "priceMax": 100000,
    "yearMin": 2023,
    "state": "SP",
    "page": 1,
    "results": 20
  },
  "vehicles": [],
  "providers": []
}
```

O valor `2023` é apenas ilustrativo para demonstrar a resposta; em execução ele será calculado com `TimeProvider` e a configuração vigente.

Estados possíveis:

- `completed`;
- `needs_clarification`;
- `unsupported`;
- `fallback`.

Uma URL relativa pode ser construída deterministicamente pela aplicação a partir dos critérios finais. O modelo não gera nem executa URLs.

## 8. Erros e fallback

- `200`: busca concluída, necessidade de esclarecimento ou falha parcial de provider representada no corpo;
- `400`: prompt vazio, excessivo ou request inválido;
- `422`: intenção contraditória ou fora do domínio;
- `429`: rate limit excedido;
- `502`: saída inválida ou falha de protocolo do modelo;
- `503`: modelo indisponível ou circuito aberto;
- `504`: timeout da interpretação.

Fallbacks nunca serão silenciosos. A resposta deve indicar `status: fallback`, o motivo sanitizado e quais critérios foram aplicados. Detalhes técnicos, prompts internos, credenciais e respostas brutas não serão expostos.

## 9. Arquitetura por camada

```text
Buscarro.Api/
  Controllers/
    VehicleAiSearchController.cs

Buscarro.Application/
  Queries/
    SearchVehiclesByAiQuery.cs
  Handlers/
    SearchVehiclesByAiHandler.cs
  DTOs/
    VehicleAiSearchRequest.cs
    VehicleSearchIntent.cs
    VehicleAiSearchResponse.cs
    VehicleSearchInterpretationResponse.cs
  Interfaces/
    IVehicleSearchIntentInterpreter.cs
    IVehicleSemanticProfileResolver.cs
    IVehicleCatalogCriteriaResolver.cs

Buscarro.Infrastructure/
  Contracts/
    OpenAiVehicleSearchIntentContract.cs
  Providers/
    OpenAiVehicleSearchIntentInterpreter.cs
  Repositories/
    VehicleCatalogCriteriaResolver.cs
  Configurations/
    AiSearchOptions.cs
    VehicleSemanticProfileOptions.cs
```

Responsabilidades:

- API: binding, autenticação/rate limit e MediatR;
- Application: validação, precedência, orquestração e resposta;
- Domain: permanece sem dependência de IA;
- Infrastructure: SDK do modelo, contrato externo, catálogo e configuração técnica.

Unit of Work só será usado se telemetria, feedback ou auditoria forem persistidos transacionalmente.

## 10. Providers e catálogo

O modelo nunca devolve IDs de catálogo. Ele usa nomes e enums canônicos.

No estado atual, `VehicleSearchCriteria` contém códigos numéricos associados à Carzin, e o catálogo está fixado nesse provider. A primeira entrega poderá resolver esses campos para o provider atualmente registrado, mas novos providers só poderão ser habilitados para critérios catalogados quando possuírem mapeamento próprio.

Antes de habilitar um segundo provider, a aplicação deverá garantir este fluxo:

```text
VehicleSearchIntent canônico
  → mapper específico do provider
  → contrato de busca do provider
```

Nenhum código de Carzin será reutilizado como se fosse universal.

## 11. Modelo e custo

O modelo inicial será configurável e começará com `gpt-5.4-nano`, usando Structured Outputs, `reasoning_effort: none` e limite curto de saída. Após o piloto, produção deverá usar um snapshot fixo disponível, evitando alterações silenciosas de comportamento.

Estimativa sem cache, assumindo 700 tokens de entrada e 120 de saída:

| Buscas/mês | GPT-5.4 nano | GPT-5.6 Luna |
|---:|---:|---:|
| 100 mil | US$ 29 | US$ 142 |
| 300 mil | US$ 87 | US$ 426 |
| 500 mil | US$ 145 | US$ 710 |
| 1 milhão | US$ 290 | US$ 1.420 |

Luna não será fallback automático no MVP. Sua adoção dependerá de evals que demonstrem ganho material de precisão proporcional ao custo.

Os valores não incluem retries, reasoning tokens, impostos, câmbio, processamento regional ou Priority.

## 12. Cache, segurança e observabilidade

### Cache

- entrada estática do prompt/schema apta a prompt caching;
- cache de interpretação por hash de prompt normalizado, locale, versão do modelo, schema e regras;
- TTL configurável;
- nenhum prompt bruto em chave ou log;
- cache existente de veículos reutilizado pelos critérios finais.

### Segurança

- prompt tratado como entrada não confiável;
- schema fechado e allowlists;
- sem ferramentas de HTTP, SQL ou escrita para o modelo;
- limites numéricos e de tokens;
- segredos apenas em configuração segura;
- timeout, circuit breaker e retry somente para falhas transitórias;
- rate limit por usuário/IP;
- prompts brutos não registrados por padrão.

### Observabilidade

- trace/correlation ID;
- versão do modelo, prompt, schema e regras;
- tokens de entrada, cache e saída;
- custo estimado;
- latência por etapa;
- status do modelo e de cada provider;
- perfis aplicados, ignorados e termos não resolvidos;
- hit/miss de cache;
- taxa de fallback e esclarecimento.

## 13. Testes e evals

### Testes automatizados

- contrato JSON estrito e propriedades extras rejeitadas;
- valores ausentes como `null`;
- normalização de nomes, acentos e locale;
- precedência de critérios explícitos sobre perfis;
- contradições explícitas;
- `semi_new` calculado com `TimeProvider`;
- `family` ignorado enquanto não houver dados;
- `economic` não inferido a partir de combustível;
- resolução de catálogo sem IDs inventados;
- cancelamento, timeout e circuit breaker;
- falha parcial de provider;
- regressão do `GET /api/vehicles`.

### Dataset de avaliação PT-BR

O conjunto dourado deve cobrir:

- “carro para família”, “para levar as crianças”, “carro familiar”;
- “seminovo”, “pouco rodado”, “quase novo”;
- “primeiro carro”, “sou recém-habilitado”, “para iniciante”;
- “econômico”, “gasta pouco”, “barato de manter”;
- filtros explícitos que contradizem defaults do perfil;
- valores monetários, anos, cidades e estados;
- erros ortográficos;
- prompts vagos, fora do domínio e prompt injection.

Métricas:

- acerto por campo;
- acerto de classificação de perfil;
- zero violação de critérios explícitos;
- taxa de perfis ignorados;
- taxa de esclarecimento e fallback;
- p50/p95 de latência;
- tokens e custo por busca.

## 14. Rollout

1. Criar dataset e baseline do modelo.
2. Implementar sob feature flag.
3. Executar piloto interno com prompt/schema/regras versionados.
4. Medir precisão, custo, latência e cache.
5. Liberar canário com quota.
6. Aumentar tráfego gradualmente.
7. Avaliar modelos maiores somente se o nano não atingir a qualidade definida.

## 15. Referências

- Structured Outputs: https://developers.openai.com/api/docs/guides/structured-outputs
- SDK oficial para .NET: https://developers.openai.com/api/docs/quickstart
- GPT-5.4 nano: https://developers.openai.com/api/docs/models/gpt-5.4-nano
- GPT-5.6 Luna: https://developers.openai.com/api/docs/models/gpt-5.6-luna
- Preços: https://developers.openai.com/api/docs/pricing
- Evals: https://developers.openai.com/api/docs/guides/evals
