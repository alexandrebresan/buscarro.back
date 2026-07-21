# Busca de veículos por linguagem natural — Design

**Data:** 2026-07-17

**Status:** aguardando revisão do usuário

**Última revisão:** 2026-07-21

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

Como pré-requisito, `IVehicleCatalogReadService` deverá passar a aceitar `CanonicalVehicleSearchCriteria`. O `GET /api/vehicles` continuará compatível por meio de uma adaptação dos filtros manuais para esse contrato canônico antes de chamar o serviço.

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
      → ISemanticProfileCatalog
      → IVehicleSemanticProfileResolver
      → CanonicalVehicleSearchCriteria
      → IVehicleCatalogReadService
          → cada IVehicleProvider traduz nomes/enums para seus códigos no próprio adapter/mapper
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
  "semanticProfiles": ["semi_new", "family"],
  "unresolvedTerms": []
}
```

`semanticProfiles` é uma lista ordenada pela primeira menção no prompt. O modelo não deve ordenar alfabeticamente; códigos repetidos são deduplicados preservando a primeira ocorrência. No exemplo, “seminovo” aparece antes de “família”, portanto a ordem correta é `semi_new`, `family`.

Essa regra não depende apenas do modelo. O backend valida cada código contra `ISemanticProfileCatalog`, preserva a ordem recebida, deduplica mantendo a primeira ocorrência e só então calcula `mentionOrder` sobre a lista normalizada. Códigos desconhecidos são rejeitados pela validação da intenção e nunca chegam ao merge engine.

Os códigos iniciais permitidos em `semanticProfiles` serão:

- `beginner`;
- `family`;
- `semi_new`;
- `economic`.

O enum do schema será gerado a partir do catálogo de perfis, que é a fonte única dos códigos. Novos códigos exigem atualização do catálogo, regras, exemplos e evals; o schema e seu fingerprint serão regenerados automaticamente.

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

1. Restrições e filtros explícitos do usuário vencem qualquer outra fonte, por campo.
2. Perfis são processados da esquerda para a direita, na ordem de primeira menção.
3. Entre perfis, o primeiro vence apenas os campos conflitantes; perfis posteriores continuam preenchendo campos livres.
4. Defaults técnicos são aplicados por último e somente em campos ainda livres.
5. Contradições explícitas não são corrigidas silenciosamente.
6. Um perfil sem suporte não bloqueia os posteriores e deve ser devolvido como `unsupported`; um perfil que aplica apenas parte de suas regras deve ser `partial`.

O merge será orientado por dados: cada definição do catálogo declara os campos que pretende derivar, e um engine genérico aplica precedência e registra conflitos. Não haverá `switch` por código de perfil no pipeline.

O catálogo/registry é a fonte única para código, ordem de regras, `rulesVersion`, campos derivados, capacidades requeridas e estado operacional. Na inicialização, a aplicação deve falhar cedo se houver códigos duplicados, campos desconhecidos, regra inválida, enum de schema divergente ou perfil habilitado sem as capacidades exigidas. Adicionar um perfil que use campos já suportados não muda o pipeline; adicionar um novo filtro exige evoluir DTOs, resolução canônica e os adapters dos providers que o suportarem.

### 6.2 Perfil `semi_new`

O modelo apenas classifica o perfil. O backend calcula:

```text
YearMin = TimeProvider.GetUtcNow().Year - SemiNewMaxAgeYears
```

`SemiNewMaxAgeYears` é uma configuração obrigatória para habilitar o perfil. O serviço não deve iniciar com o perfil habilitado se a configuração for inválida. Um `yearMin` explícito do usuário vence o valor derivado.

### 6.3 Perfil `family`

O sistema atual não possui critérios confiáveis de lugares, categoria de carroceria, volume do porta-malas ou segurança. Portanto, no MVP o modelo pode reconhecer `family`, mas o backend deve devolvê-lo somente em `profileResolutions`, com `status: unsupported` e o código `FAMILY_PROFILE_UNSUPPORTED` em `skippedFields.reason`.

O perfil só poderá ser habilitado quando existirem dados e filtros confiáveis. Não será permitido traduzir automaticamente `family` para SUV ou para marcas/modelos específicos. Uma flag de configuração, sozinha, não pode habilitá-lo: o registry deve validar também as capacidades de catálogo e provider. Enquanto estiver `unsupported`, sua presença não impede que perfis posteriores sejam processados.

### 6.4 Perfil `beginner`

O perfil pode preencher apenas critérios configurados e já suportados, como `priceMax` e transmissão. Os valores devem ser definidos pelo produto em configuração versionada. Se o usuário solicitar transmissão automática, essa escolha vence um default manual do perfil.

### 6.5 Perfil `economic`

`Fuel` não representa consumo. Sem km/l, custo de manutenção ou outro indicador confiável, no MVP o perfil será reconhecido e devolvido em `profileResolutions` com `status: unsupported` e `ECONOMIC_PROFILE_UNSUPPORTED`. O sistema não afirmará que um veículo é econômico com base apenas no combustível. `partial` só é válido quando existir pelo menos um item em `appliedFields` e pelo menos um em `skippedFields`.

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
      "yearMin": {
        "value": 2023,
        "source": "semantic_profile:semi_new",
        "rulesVersion": "2026-07-17"
      }
    },
    "profileResolutions": [
      {
        "code": "semi_new",
        "mentionOrder": 0,
        "status": "applied",
        "rulesVersion": "2026-07-17",
        "appliedFields": ["yearMin"],
        "skippedFields": []
      },
      {
        "code": "family",
        "mentionOrder": 1,
        "status": "unsupported",
        "rulesVersion": "2026-07-17",
        "appliedFields": [],
        "skippedFields": [
          {
            "field": "*",
            "reason": "FAMILY_PROFILE_UNSUPPORTED",
            "winner": null
          }
        ]
      }
    ],
    "finalCriteriaProvenance": {
      "transmission": "explicit",
      "priceMax": "explicit",
      "state": "explicit",
      "yearMin": "semantic_profile:semi_new"
    },
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

O valor `2023` é apenas ilustrativo para demonstrar a resposta; em execução ele será calculado com `TimeProvider` e a configuração vigente. Cada profile deve informar `status` (`applied`, `partial` ou `unsupported`), `mentionOrder`, `appliedFields` e `skippedFields`. `partial` exige ao menos um campo aplicado; sem nenhum `appliedField`, o profile é `unsupported`. Um campo ignorado por conflito deve incluir `reason` e o `winner` (`explicit` ou outro profile). Defaults não disputam campos: são aplicados somente aos livres e aparecem apenas na proveniência final. A proveniência dos critérios derivados e finais deve permitir explicar a decisão sem expor o prompt de sistema.

Estados possíveis:

- `completed`;
- `needs_clarification`;
- `unsupported`;
- `fallback`.

O status agregado será `completed` sempre que restar uma busca executável, mesmo que uma ou mais resoluções de profile sejam `unsupported`. Será `unsupported` somente quando nenhuma intenção aplicável restar. `needs_clarification` será usado quando a busca depender de uma ambiguidade que não possa ser resolvida com segurança; `fallback`, quando o caminho alternativo explícito for acionado após falha do modelo ou de sua validação.

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
  Configurations/
    AiSearchConfiguration.cs

Buscarro.Application/
  Queries/
    SearchVehiclesByAiQuery.cs
  Handlers/
    SearchVehiclesByAiHandler.cs
  Rules/
    VehicleSemanticProfileResolver.cs
    SemanticProfileMergeEngine.cs
  DTOs/
    VehicleAiSearchRequest.cs
    VehicleSearchIntent.cs
    CanonicalVehicleSearchCriteria.cs
    VehicleAiSearchResponse.cs
    VehicleSearchInterpretationResponse.cs
  Interfaces/
    IVehicleSearchIntentInterpreter.cs
    ISemanticProfileCatalog.cs
    IVehicleSemanticProfileResolver.cs

Buscarro.Infrastructure/
  Contracts/
    OpenAiVehicleSearchIntentContract.cs
  Providers/
    OpenAiVehicleSearchIntentInterpreter.cs
    ConfiguredSemanticProfileCatalog.cs
    AiSearchOptions.cs
    VehicleSemanticProfileOptions.cs
    CarzinVehicleSearchCriteriaMapper.cs
```

Responsabilidades:

- API: binding, autenticação/rate limit e MediatR;
- Application: validação, precedência, orquestração e resposta;
- Domain: permanece sem dependência de IA;
- Infrastructure: SDK do modelo, contrato externo, implementação configurada do catálogo/registry e configuração técnica.

`ISemanticProfileCatalog`, definido na Application, expõe definições canônicas sem tipos do SDK ou de configuração. `ConfiguredSemanticProfileCatalog`, na Infrastructure, materializa e valida essas definições a partir das opções. `VehicleSemanticProfileResolver` e `SemanticProfileMergeEngine` ficam na extensão arquitetural focada `Application/Rules`, pois precedência e proveniência são regras determinísticas de aplicação; `Application/Handlers` permanece reservado aos Handlers MediatR. As opções técnicas ficam junto das implementações em `Infrastructure/Providers`, enquanto binding, validação de inicialização e registro de DI ficam em `Api/Configurations`.

Não haverá um resolvedor central de catálogo. A Application entrega `CanonicalVehicleSearchCriteria` ao catálogo; cada implementação de `IVehicleProvider` é responsável por traduzir nomes e enums canônicos para seus próprios códigos, usando mapper/adapter isolado em `Infrastructure/Providers`.

Unit of Work só será usado se telemetria, feedback ou auditoria forem persistidos transacionalmente.

## 10. Providers e catálogo

O modelo nunca devolve IDs de catálogo. Ele usa nomes e enums canônicos. O objeto público `criteria` também é estritamente canônico e nunca expõe IDs ou códigos de provider.

No estado atual, `VehicleSearchCriteria` contém códigos numéricos associados à Carzin, e o catálogo está fixado nesse provider. Evoluir esse contrato interno para `CanonicalVehicleSearchCriteria` (ou equivalente) aceito por `IVehicleCatalogReadService` é pré-requisito da feature. A feature de IA não deve perpetuar o acoplamento: nomes/enums permanecem canônicos na Application e são traduzidos para códigos Carzin somente dentro de `CarzinVehicleSearchCriteriaMapper`. Mesmo na primeira entrega com um único provider, IDs específicos permanecem internos. Novos providers só poderão ser habilitados para critérios catalogados quando possuírem mapper próprio.

Antes de habilitar um segundo provider, a aplicação deverá garantir este fluxo:

```text
VehicleSearchIntent/criteria canônico
  → adapter específico do provider, com tradução interna de IDs
  → contrato de busca do provider
```

Nenhum código de Carzin será reutilizado como se fosse universal.

## 11. Modelo e custo

O baseline configurável será `gpt-4o-mini`, usando Structured Outputs e limite curto de saída. Não será enviado `reasoning_effort`, pois esse parâmetro não faz parte deste desenho. Após o piloto, produção deverá fixar um snapshot do modelo disponível, evitando alterações silenciosas de comportamento.

Estimativa sem cache, assumindo 700 tokens de entrada e 120 de saída:

| Buscas/mês | `gpt-4o-mini` |
|---:|---:|
| 100 mil | US$ 17,70 |
| 300 mil | US$ 53,10 |
| 500 mil | US$ 88,50 |
| 1 milhão | US$ 177,00 |

O cálculo usa os preços atuais de US$ 0,15 por 1 milhão de tokens de entrada, US$ 0,075 por 1 milhão de tokens de entrada em cache e US$ 0,60 por 1 milhão de tokens de saída. A tabela é deliberadamente sem cache. Preços e consumo real deverão ser revalidados antes do rollout e medidos em produção.

Modelos maiores ficam somente como alternativas e não serão fallback automático. Sua adoção dependerá de evals que demonstrem ganho material de precisão proporcional a custo e latência. Os valores não incluem retries, impostos, câmbio, processamento regional ou Priority.

## 12. Cache, segurança e observabilidade

### Cache

- entrada estática do prompt/schema apta a prompt caching;
- `inputFingerprint`, preferencialmente um HMAC com chave do servidor sobre prompt normalizado e locale, sem armazenar ou permitir reconstruir o texto bruto;
- `systemPromptFingerprint`, calculado como hash do prompt de sistema, few-shots e configuração relevante à interpretação;
- cache somente da intenção retornada pelo modelo e validada pelo backend, usando `inputFingerprint`, `systemPromptFingerprint`, snapshot do modelo e fingerprint do catálogo/schema/regras;
- critérios temporais derivados não são armazenados nesse cache: `semi_new` é recalculado com `TimeProvider` e as regras vigentes a cada request;
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
- `inputFingerprint`, sem prompt bruto, e `systemPromptFingerprint`;
- snapshot do modelo e fingerprint do catálogo/schema/regras;
- tokens de entrada, cache e saída;
- custo estimado;
- latência por etapa;
- status do modelo e de cada provider;
- ordem mencionada, perfis `applied`/`partial`/`unsupported`, campos aplicados/pulados, winners e termos não resolvidos;
- hit/miss de cache;
- taxa de fallback e esclarecimento.

## 13. Testes e evals

### Testes automatizados

- contrato JSON estrito e propriedades extras rejeitadas;
- valores ausentes como `null`;
- normalização de nomes, acentos e locale;
- precedência de critérios explícitos sobre perfis;
- preservação da ordem da primeira menção e ausência de ordenação alfabética;
- deduplicação preservando a primeira ocorrência;
- validação backend dos códigos e cálculo de `mentionOrder` após normalização;
- múltiplos perfis processados da esquerda para a direita;
- conflitos parciais em que o profile posterior ainda preenche campos livres;
- contradições explícitas;
- `semi_new` calculado com `TimeProvider`;
- hit de intenção em cache com `semi_new` recalculado em requests de datas diferentes;
- `family` como `unsupported`, sem bloquear profiles posteriores e sem habilitação apenas por config;
- `economic` não inferido a partir de combustível;
- consistência do registry, geração do enum e validação de startup;
- invalidação do cache quando `systemPromptFingerprint` ou o fingerprint do catálogo/schema/regras mudar, mantendo `inputFingerprint` estável para a mesma entrada;
- miss/invalidação da entrada em cache quando prompt normalizado ou locale alterarem o `inputFingerprint`;
- geração determinística e proteção do `inputFingerprint` por HMAC;
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
- múltiplos perfis em ordens distintas, repetidos e com conflitos parciais;
- “seminovo para família e para iniciante”, validando `semi_new`, `family`, `beginner` nessa ordem e a continuidade após `family` unsupported;
- valores monetários, anos, cidades e estados;
- erros ortográficos;
- prompts vagos, fora do domínio e prompt injection.

Métricas:

- acerto por campo;
- acerto de classificação de perfil;
- exact match da sequência ordenada de profiles;
- acerto da deduplicação e de `mentionOrder` normalizado;
- acerto de winner e proveniência por campo;
- taxa de profiles posteriores que preenchem corretamente campos livres após conflitos;
- continuidade correta do pipeline após profile `unsupported`;
- zero violação de critérios explícitos;
- taxas de profiles `unsupported` e `partial`;
- taxa de esclarecimento e fallback;
- p50/p95 de latência;
- tokens e custo por busca.

## 14. Rollout

1. Criar dataset e baseline com `gpt-4o-mini`.
2. Implementar registry, validação de startup e geração do schema sob feature flag.
3. Executar piloto interno com snapshot, `inputFingerprint`, `systemPromptFingerprint` e fingerprint do catálogo/schema/regras observáveis e versionados quando aplicável.
4. Medir precisão, custo real, latência, ordem dos profiles e cache.
5. Liberar canário com quota.
6. Aumentar tráfego gradualmente.
7. Avaliar modelos maiores somente se o baseline não atingir a qualidade definida nos evals.

## 15. Referências

- Structured Outputs: https://developers.openai.com/api/docs/guides/structured-outputs
- SDK oficial para .NET: https://developers.openai.com/api/docs/quickstart
- GPT-4o mini: https://developers.openai.com/api/docs/models/gpt-4o-mini
- Preços: https://developers.openai.com/api/docs/pricing
- Evals: https://developers.openai.com/api/docs/guides/evals
