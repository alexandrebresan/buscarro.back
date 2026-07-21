# Busca de veículos por linguagem natural — Design

**Data:** 2026-07-17

**Status:** aguardando revisão do usuário

**Última revisão:** 2026-07-21

**Escopo:** tradução de prompt em navegação canônica; não inclui ranking semântico

## 1. Objetivo

Adicionar ao Buscarro uma entrada em linguagem natural que traduza o pedido do usuário em uma URL relativa, segura e reproduzível para a busca convencional.

O fluxo será deliberadamente dividido em dois requests:

1. o frontend envia o prompt ao `POST /api/vehicles/ai-search`;
2. o backend interpreta o prompt e devolve uma `searchUrl` relativa;
3. o frontend chama `GET {searchUrl}` para obter veículos normalizados e status dos providers.

O POST é apenas um tradutor de prompt para navegação. Ele não consulta veículos, não chama `IVehicleCatalogReadService` e não retorna veículos ou status de provider.

A resposta do POST contém obrigatoriamente:

- `aiSearch: true`;
- o prompt normalizado;
- `searchUrl` relativa;
- warnings e metadados de interpretação quando aplicáveis.

Os dois roundtrips são um trade-off aceito: preservam uma única execução da busca, permitem que a URL seja editada, compartilhada e repetida e mantêm o GET como fonte única dos resultados.

## 2. Não objetivos

O MVP não inclui:

- consulta de veículos ou providers pelo POST de IA;
- retorno de `criteria`, `vehicles` ou `providers` pelo POST;
- geração de URL pelo modelo;
- URL absoluta, redirect HTTP ou navegação executada pelo backend;
- ordenação por relevância semântica;
- recomendação personalizada;
- RAG, embeddings ou banco vetorial;
- fine-tuning;
- memória conversacional;
- agente autônomo;
- acesso do modelo a HTTP, SQL, catálogo ou providers;
- fuzzy matching que escolha silenciosamente uma entidade;
- IDs de provider no contrato canônico ou na URL gerada pela IA;
- inferência de consumo, segurança ou custo de manutenção sem dados confiáveis.

## 3. Decisões principais

### 3.1 POST tradutor e GET executor

O endpoint de IA será:

```http
POST /api/vehicles/ai-search
```

O Handler interpreta a intenção, aplica regras semânticas e usa o output port `IVehicleSearchUrlBuilder` para montar a resposta completa. A interface pertence à Application e opera somente com DTOs/string da própria Application, sem tipos ASP.NET. Sua implementação `VehicleSearchUrlBuilder` permanece na borda da API. O frontend usa a URL retornada para chamar:

```http
GET /api/vehicles
```

Somente o GET chama `IVehicleCatalogReadService` e os providers.

### 3.2 URL determinística

`VehicleSearchUrlBuilder` gera a URL no backend, nunca no LLM. O builder deve:

- aceitar apenas uma allowlist de campos canônicos;
- usar path fixo `/api/vehicles`;
- aplicar percent-encoding correto;
- serializar números com `InvariantCulture`;
- produzir ordem estável dos parâmetros;
- omitir somente valores `null` ou vazios;
- nunca emitir host, scheme, fragment, credenciais ou IDs de provider.

Um critério explícito jamais é descartado por ser “não suportado”: se sua dimensão global estiver habilitada, o builder deve serializá-lo; se estiver desabilitada, o POST deve rejeitá-lo explicitamente com `422`. A allowlist é gerada a partir do registry de dimensões globalmente habilitadas.

A URL não será armazenada em cache: será reconstruída a cada request a partir da intenção validada e das regras atuais.

### 3.3 Contrato canônico e transição

O GET passará a aceitar estes parâmetros canônicos:

- `manufacturer_name`;
- `model_name`;
- `version_name`;
- `city_name`;
- `color_name`;
- `gear_name`;
- `fuel_name`.

Os parâmetros numéricos atuais serão preservados temporariamente para compatibilidade. Se o request informar a forma canônica e a numérica do mesmo campo, o GET retorna `400`, mesmo que os valores aparentem ser equivalentes.

O POST gera exclusivamente parâmetros `*_name`; nunca gera a forma numérica legada.

### 3.4 Uma chamada ao modelo

O modelo terá uma única responsabilidade: extrair intenção explícita e classificar perfis semânticos em Structured Outputs. Ele não produz URLs, IDs, objetos de provider ou veículos.

Regras subjetivas permanecem determinísticas no backend e são aplicadas sobre critérios canônicos.

### 3.5 Registry de dimensões

`IVehicleSearchDimensionRegistry` é a fonte única para códigos de dimensão, nomes canônicos, parâmetros de URL, dependências de pais, estado habilitado, ordem de serialização e versão/fingerprint. Ele é consumido por:

- `SearchVehiclesByAiHandler`, para rejeitar dimensão desabilitada;
- schema factory/interpreter, para gerar somente campos habilitados;
- `VehicleSearchUrlBuilder`, para gerar allowlist e ordem estável;
- validações de startup, cache, observabilidade e testes.

`ConfiguredVehicleSearchDimensionRegistry`, em Infrastructure/Providers, materializa a configuração e falha no startup se houver nomes ou parâmetros duplicados, hierarquia inválida, ordem ambígua ou divergência entre schema e allowlist. Nenhum consumidor mantém uma lista paralela de dimensões.

## 4. Fluxos

### 4.1 Tradução por IA

```text
Frontend
  → POST /api/vehicles/ai-search
  → VehicleAiSearchController
  → SearchVehiclesByAiQuery
  → SearchVehiclesByAiHandler
      → cache de intenção validada
      → IVehicleSearchIntentInterpreter
      → validação e normalização da intenção
      → ISemanticProfileCatalog
      → IVehicleSemanticProfileResolver
      → IVehicleSearchDimensionRegistry
      → CanonicalVehicleSearchCriteria
      → IVehicleSearchUrlBuilder
          → Api/Routing/VehicleSearchUrlBuilder
  → VehicleAiSearchResponse
  → aiSearch + prompt normalizado + searchUrl + warnings/metadata
```

### 4.2 Execução da busca

```text
Frontend
  → GET {searchUrl}
  → VehiclesController
  → VehicleSearchRequest
  → IVehicleSearchRequestCanonicalizer
      → parâmetros *_name permanecem canônicos
      → IDs legados Carzin são resolvidos para nomes canônicos
  → CanonicalVehicleSearchCriteria
  → FindAllVehiclesQuery
  → FindAllVehiclesHandler
  → IVehicleCatalogReadService
      → cada IVehicleProvider recebe critérios canônicos
      → mapper do provider resolve nomes/enums para seus ProviderCodes
  → veículos normalizados + status/unsupportedCriteria por provider
```

O `CancellationToken` deve ser preservado em todas as etapas.

`VehicleAiSearchController` permanece fino: sua ação equivale a `Ok(await mediator.Send(...))`, preservando apenas binding, cancellation e tradução de exceções para status HTTP.

## 5. Intenção e perfis semânticos

### 5.1 JSON Schema estrito

O modelo responde com schema estrito:

- todos os campos definidos como obrigatórios;
- ausências representadas por `null`;
- `additionalProperties: false`;
- enums fechados;
- `semanticProfiles` limitado aos códigos do registry;
- nenhuma propriedade para URL, IDs, providers ou veículos.

Exemplo conceitual:

```json
{
  "explicitCriteria": {
    "query": null,
    "manufacturerName": "Honda",
    "modelName": "Civic",
    "versionName": null,
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

`semanticProfiles` preserva a ordem da primeira menção. O backend valida os códigos contra `ISemanticProfileCatalog`, mantém a ordem recebida, deduplica preservando a primeira ocorrência e calcula `mentionOrder` após a normalização.

O schema factory gera o schema ativo exclusivamente a partir de `IVehicleSearchDimensionRegistry`, contendo somente dimensões globalmente habilitadas. Manufacturer, model e version estão habilitados com o catálogo atual. City, color, gear e fuel permanecem fora do schema até passarem pelo gate de capacidades. Quando gear for habilitado, o nome canônico será sempre `gearName` no schema, nos critérios, nas regras de profile e na URL; `transmission` não será usado como campo alternativo.

O POST valida schema, intervalos, contradições semânticas e a presença estrutural dos pais (`manufacturerName` para model/version, `modelName` para version e `state` para city). Ele não decide se nomes existem, se a combinação consta do catálogo ou se há ambiguidade: essa validação autoritativa ocorre no GET e nos mappers dos providers. Portanto, uma URL sintaticamente válida produzida pelo POST ainda pode resultar em `422` ou falha de provider no GET.

### 5.2 Precedência

1. Critérios explícitos vencem qualquer outra fonte, por campo.
2. Profiles são processados da esquerda para a direita.
3. Entre profiles, o primeiro vence somente campos conflitantes; posteriores preenchem campos livres.
4. Defaults são aplicados por último e somente a campos livres.
5. Defaults aparecem na proveniência final, mas nunca como winner de conflito.
6. Um profile `unsupported` não bloqueia os posteriores.
7. `partial` exige ao menos um `appliedField` e um `skippedField`.

O merge engine é genérico e orientado pelo registry, sem `switch` por profile.

### 5.3 Profiles iniciais

- `semi_new`: deriva `yearMin` com `TimeProvider` e configuração versionada; o cálculo ocorre em todo POST, inclusive após cache hit da intenção;
- `beginner`: pode preencher somente campos canônicos suportados e configurados; qualquer regra de câmbio usa exclusivamente `gearName` quando essa dimensão estiver habilitada;
- `family`: `unsupported` no MVP por falta de dados confiáveis;
- `economic`: `unsupported` no MVP; combustível não comprova economia.

`family` ou `economic` sozinhos não deixam intenção executável e resultam em `422`. Quando aparecem junto de filtros aplicáveis, o POST pode retornar `200`, com warning e resolução `unsupported`, sem bloquear a URL.

`unresolvedTerms` vira warnings somente quando os critérios restantes permitem gerar uma URL fiel. Se o termo não resolvido for indispensável ao pedido ou não restar intenção aplicável, o POST retorna `422` em vez de produzir uma navegação enganosa.

Nenhum profile pode ser habilitado apenas por configuração quando as capacidades requeridas não existem no catálogo e nos providers.

## 6. Contrato do POST

### 6.1 Requisição

```json
{
  "prompt": "Quero um Honda Civic seminovo para família, até 100 mil em SP",
  "page": 1,
  "results": 20,
  "locale": "pt-BR"
}
```

Validações:

- prompt obrigatório, normalizado e limitado;
- `page >= 1`;
- `results` dentro do limite configurado;
- locale em allowlist;
- intervalos coerentes;
- presença estrutural dos pais nas hierarquias canônicas;
- nenhuma contradição explícita irreconciliável.

Existência, combinação e ambiguidade no catálogo não são validadas pelo POST.

### 6.2 Resposta

```json
{
  "aiSearch": true,
  "prompt": "Quero um Honda Civic seminovo para família, até 100 mil em SP",
  "searchUrl": "/api/vehicles?page=1&results=20&state=SP&manufacturer_name=Honda&model_name=Civic&price_max=100000&year_min=2023",
  "warnings": [
    {
      "code": "FAMILY_PROFILE_UNSUPPORTED"
    }
  ],
  "metadata": {
    "status": "completed",
    "profileResolutions": [
      {
        "code": "semi_new",
        "mentionOrder": 0,
        "status": "applied",
        "rulesVersion": "2026-07-21",
        "appliedFields": ["yearMin"],
        "skippedFields": []
      },
      {
        "code": "family",
        "mentionOrder": 1,
        "status": "unsupported",
        "rulesVersion": "2026-07-21",
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
      "manufacturerName": "explicit",
      "modelName": "explicit",
      "priceMax": "explicit",
      "state": "explicit",
      "yearMin": "semantic_profile:semi_new"
    }
  }
}
```

O valor `2023` é apenas ilustrativo; a execução usa `TimeProvider`. O prompt retornado é normalizado, mas continua sendo entrada não confiável. O frontend deve renderizá-lo como texto, nunca como HTML.

### 6.3 Status HTTP do POST

- `200`: intenção aplicável e `searchUrl` construída;
- `400`: request inválido ou prompt vazio/excessivo;
- `422`: contradição semântica, pai estrutural ausente, dimensão global desabilitada, termo indispensável não resolvido ou nenhuma intenção aplicável;
- `429`: rate limit excedido;
- `502`: saída inválida ou falha de protocolo do modelo;
- `503`: modelo indisponível ou circuito aberto;
- `504`: timeout da interpretação.

O POST nunca mascara falha do modelo com URL inventada.

## 7. Contrato e canonicalização do GET

`VehicleSearchRequest` será o contrato HTTP e ficará separado de `CanonicalVehicleSearchCriteria`.

Por compatibilidade, `VehicleSearchRequest` é a única exceção transitória em Application/DTOs que pode conter IDs legados Carzin, exclusivamente na entrada HTTP. O canonicalizer elimina esses IDs antes de criar a Query canônica e antes de qualquer chamada ao serviço ou provider. Nenhum outro DTO, Query, cache key ou caminho após a canonicalização pode conter IDs específicos da Carzin.

Além dos novos campos `*_name`, ele preserva durante a transição os campos atuais, como `manufacturer`, `model`, `version`, `city`, `color`, `gear` e `fuel`, junto de paginação, preço, ano, estado, query e ordenação.

`IVehicleSearchRequestCanonicalizer` executa:

1. valida conflito canônico + numérico por campo e retorna `400`;
2. normaliza nomes, enums, estado, intervalos e paginação de forma genérica;
3. valida a presença estrutural dos pais;
4. quando houver IDs legados, delega exclusivamente a `ILegacyVehicleSearchCriteriaAdapter`;
5. produz `CanonicalVehicleSearchCriteria`;
6. usa somente os critérios canônicos para a chave do cache de veículos.

`CarzinLegacyVehicleSearchCriteriaAdapter` é a implementação transitória de ProviderCode → nome para os parâmetros numéricos atuais. Ela não faz o caminho nome → ProviderCode e não concentra resolução dos demais providers. Cada mapper de provider continua responsável pela resolução canônica autoritativa de nomes e pela validação de existência, combinação e ambiguidade.

Assim, requests equivalentes por ID legado e por nome convergem para a mesma chave e o mesmo resultado canônico.

Ausência estrutural do pai necessário retorna `422` no canonicalizer. Entidade inexistente, combinação hierárquica inválida ou ambígua é detectada autoritativamente no GET/provider e resulta em `422` quando a rejeição atingir todos os providers aplicáveis. Matching aproximado pode sugerir esclarecimento, mas nunca seleciona um valor silenciosamente.

## 8. URL de busca

A ordem estável base dos parâmetros habilitados será:

```text
query, order, page, results, state,
manufacturer_name, model_name, version_name,
price_min, price_max, year_min, year_max
```

Após o gate de capacidades, campos opcionais entram em posições estáveis: `city_name` após state e `color_name`, `gear_name`, `fuel_name` após year_max. O builder usa somente a allowlist gerada das dimensões habilitadas. Campos futuros exigem atualização explícita do contrato, registry, testes e capacidades dos providers.

Exemplos de garantias:

- `São Paulo` é codificado como query string válida;
- decimais não dependem da cultura da máquina;
- ordem de propriedades do objeto não altera a URL;
- `null` não gera parâmetro;
- todo critério explícito de dimensão habilitada gera parâmetro;
- a saída começa com `/api/vehicles?`;
- a saída não contém `://`, `#`, host ou parâmetros numéricos de catálogo.

## 9. Catálogo e providers

`IVehicleCatalogReadService` e `IVehicleProvider` passam a receber `CanonicalVehicleSearchCriteria`. Cada provider traduz nomes/enums canônicos para `ProviderCode` dentro de seu próprio mapper em Infrastructure.

Para Carzin:

- manufacturer, model e version usam as tabelas atuais;
- city precisa de tabela e sincronização escopada por state;
- color, gear e fuel precisam de tabelas e sincronização próprias;
- migrations, contratos de seed e paths externos desses catálogos são dependências explícitas do MVP antes de habilitar os filtros correspondentes.

Não serão aceitos IDs inventados, dicionários mágicos em código ou omissão silenciosa de filtros. Se um provider não puder mapear um critério, seu resultado será uma falha parcial com `unsupportedCriteria` detalhado e sanitizado; os demais providers continuam.

O mapper valida e resolve todos os critérios antes da chamada externa. Se rejeitar algum deles, o provider não é chamado, retorna zero veículos e `Success = false` com `unsupportedCriteria` preenchido.

### 9.1 Resolução hierárquica

- ordem: `manufacturer → model → version` e `state → city`;
- primeiro matching exato após normalização de case, acentos e whitespace;
- um filho exige o pai;
- múltiplos matches são ambíguos;
- ausência do pai ou ambiguidade resulta em `422`;
- fuzzy matching nunca escolhe silenciosamente;
- nomes não mapeáveis ou capacidades ausentes em um provider aparecem em `unsupportedCriteria`, nunca são descartados.

O adaptador legado ProviderCode → nome é exclusivo da transição do GET. O caminho canônico nome → ProviderCode continua isolado no mapper de cada provider.

### 9.2 Status dos providers e status agregado do GET

`ProviderStatusResponse` terá o contrato completo:

```json
{
  "provider": "Carzin",
  "success": false,
  "fromCache": false,
  "usedStaleCache": false,
  "error": "Critérios não suportados por este provider.",
  "currentResults": 0,
  "currentPage": 1,
  "unsupportedCriteria": [
    {
      "field": "cityName",
      "value": "São Paulo",
      "reason": "CATALOG_DIMENSION_UNAVAILABLE"
    }
  ]
}
```

Agregação HTTP do GET:

- `200` se ao menos um provider tiver `Success = true`, inclusive em sucesso parcial;
- `422` se todos os providers aplicáveis rejeitarem os critérios antes da chamada externa;
- `503` se nenhum provider tiver sucesso e todos os providers que aceitaram os critérios falharem por indisponibilidade;
- rejeições e indisponibilidades permanecem discriminadas por provider no corpo, sem converter uma em outra.

## 10. Arquitetura por camada

```text
Buscarro.Api/
  Controllers/
    VehicleAiSearchController.cs
    VehiclesController.cs
  Routing/
    VehicleSearchUrlBuilder.cs
  Configurations/
    AiSearchConfiguration.cs
    VehicleSearchConfiguration.cs

Buscarro.Application/
  Queries/
    SearchVehiclesByAiQuery.cs
    FindAllVehiclesQuery.cs
  Handlers/
    SearchVehiclesByAiHandler.cs
    FindAllVehiclesHandler.cs
  Rules/
    VehicleSemanticProfileResolver.cs
    SemanticProfileMergeEngine.cs
    VehicleSearchRequestCanonicalizer.cs
  DTOs/
    VehicleAiSearchRequest.cs
    VehicleAiSearchResponse.cs
    VehicleSearchRequest.cs
    VehicleSearchIntent.cs
    CanonicalVehicleSearchCriteria.cs
    VehicleSearchInterpretationResponse.cs
    UnsupportedCriterionResponse.cs
  Interfaces/
    IVehicleSearchIntentInterpreter.cs
    ISemanticProfileCatalog.cs
    IVehicleSemanticProfileResolver.cs
    IVehicleSearchDimensionRegistry.cs
    IVehicleSearchUrlBuilder.cs
    IVehicleSearchRequestCanonicalizer.cs
    ILegacyVehicleSearchCriteriaAdapter.cs
    IVehicleCatalogReadService.cs
    IVehicleProvider.cs

Buscarro.Domain/
  Entities/
    VehicleProviderCity.cs
    VehicleProviderColor.cs
    VehicleProviderGear.cs
    VehicleProviderFuel.cs

Buscarro.Infrastructure/
  Contracts/
    OpenAiVehicleSearchIntentContract.cs
    CarzinCityCatalogContract.cs
    CarzinColorCatalogContract.cs
    CarzinGearCatalogContract.cs
    CarzinFuelCatalogContract.cs
  Providers/
    OpenAiVehicleSearchIntentInterpreter.cs
    OpenAiVehicleSearchSchemaFactory.cs
    ConfiguredSemanticProfileCatalog.cs
    ConfiguredVehicleSearchDimensionRegistry.cs
    AiSearchOptions.cs
    VehicleSemanticProfileOptions.cs
    CarzinLegacyVehicleSearchCriteriaAdapter.cs
    CarzinVehicleSearchCriteriaMapper.cs
    CarzinVehicleProvider.cs
    CarzinCatalogSeedSyncService.cs
  Persistence/
    Configurations/
      VehicleProviderCityConfiguration.cs
      VehicleProviderColorConfiguration.cs
      VehicleProviderGearConfiguration.cs
      VehicleProviderFuelConfiguration.cs
    Migrations/
```

Responsabilidades:

- API: binding HTTP, status, rate limit, URL relativa e registro de DI;
- Application/Handlers: somente orquestração MediatR;
- Application/Rules: precedência, merge e proveniência determinísticos;
- Application/DTOs: contratos internos e canônicos; somente `VehicleSearchRequest` admite IDs Carzin como exceção transitória de entrada;
- Domain: permanece sem IA ou contratos externos;
- Infrastructure/Contracts: contratos externos usados pelo seed/sync;
- Infrastructure/Persistence: configuração EF e migrations das entidades de catálogo do Domain;
- Infrastructure/Providers: SDK, sync, adapter legado e tradução específica por provider.

`IVehicleSearchUrlBuilder` é um output port da Application que usa apenas DTOs/string da Application. `VehicleSearchUrlBuilder`, sua implementação em `Api/Routing`, conhece o path e a serialização HTTP; o Handler chama a porta e retorna `VehicleAiSearchResponse` completa, enquanto o Controller apenas envia a Query ao MediatR e envolve o resultado em `Ok`. Antes da implementação, `AGENTS.md` deve registrar `Application/Rules` e `Api/Routing` como extensões arquiteturais permitidas, mantendo a Application sem tipos ASP.NET.

Unit of Work só será usado se houver escrita transacional de feedback, auditoria ou catálogo.

## 11. Modelo e custo

O baseline configurável será `gpt-4o-mini`, usando Structured Outputs e limite curto de saída. Não será enviado `reasoning_effort`. Produção deverá fixar um snapshot disponível.

Estimativa sem cache, assumindo 700 tokens de entrada e 120 de saída:

| Buscas/mês | `gpt-4o-mini` |
|---:|---:|
| 100 mil | US$ 17,70 |
| 300 mil | US$ 53,10 |
| 500 mil | US$ 88,50 |
| 1 milhão | US$ 177,00 |

O cálculo usa os preços atuais de US$ 0,15 por 1 milhão de tokens de entrada, US$ 0,075 por 1 milhão de tokens de entrada em cache e US$ 0,60 por 1 milhão de tokens de saída. Preços e medição real devem ser revalidados antes do rollout.

Modelos maiores só serão avaliados se evals demonstrarem ganho material de qualidade proporcional a custo e latência.

## 12. Cache

Haverá três caches separados:

### 12.1 Intenção validada

- chave com `inputFingerprint`, preferencialmente HMAC do prompt normalizado + locale;
- inclui snapshot do modelo, `systemPromptFingerprint`, versão/fingerprint do registry de profiles e `dimensionRegistryFingerprint`;
- armazena somente intenção validada;
- não armazena critérios temporais derivados nem `searchUrl`;
- `semi_new` e a URL são recalculados em todo POST.

`systemPromptFingerprint` é o hash do prompt de sistema, few-shots e configuração relevante à interpretação.

### 12.2 Catálogo bidirecional e revisionado

- separado por provider, dimensão, hierarquia e revisão do sync;
- suporta ProviderCode → nome para canonicalização legada;
- suporta nome canônico → ProviderCode nos mappers dos providers;
- invalida entradas quando a revisão do catálogo muda;
- nunca compartilha códigos entre providers.

### 12.3 Veículos por critérios canônicos

- chave construída somente após `VehicleSearchRequest` virar `CanonicalVehicleSearchCriteria`;
- requests por ID legado e nome equivalente convergem;
- mantém isolamento e status por provider;
- inclui a revisão de catálogo necessária à tradução.

## 13. Segurança e erros

- prompt é entrada não confiável;
- Structured Outputs, enums fechados e allowlists;
- o modelo não recebe ferramentas;
- URL produzida apenas pelo builder determinístico;
- nenhuma URL absoluta, fragment ou host;
- limites de prompt, tokens, tempo e resultados;
- segredos em configuração segura;
- retry somente para falhas transitórias;
- prompts brutos não são registrados por padrão;
- erros de provider são sanitizados e identificam o provider;
- `unsupportedCriteria` não expõe SQL, credenciais ou payload externo.

Como o POST ecoa o prompt normalizado, o frontend deve inseri-lo como texto escapado. É proibido usar o valor em `innerHTML` ou equivalente; testes devem cobrir payloads XSS.

## 14. Observabilidade

### POST de IA

- trace/correlation ID;
- `inputFingerprint` e `systemPromptFingerprint`, sem prompt bruto;
- snapshot, schema e versão do registry;
- versão e fingerprint de `IVehicleSearchDimensionRegistry` usados pelo Handler, schema e URL builder;
- tokens de entrada/cached input/saída e custo estimado;
- latência do modelo, regras e URL builder;
- cache hit/miss da intenção;
- profiles `applied`/`partial`/`unsupported`;
- warnings, status HTTP e taxa de `422`;
- validação de que o POST não executou busca de veículos.

### GET de veículos

- origem da navegação, incluindo `aiSearch` apenas como contexto seguro do frontend/telemetria, não como critério;
- modo de entrada canônico ou legado;
- resultado da canonicalização e revisão do catálogo;
- convergência/hit do cache canônico;
- latência e status por provider;
- `unsupportedCriteria` por provider;
- falhas de hierarquia e ambiguidades.

## 15. Testes e evals

### 15.1 POST e URL

- resposta contém obrigatoriamente `aiSearch: true`, prompt normalizado e `searchUrl`;
- resposta não contém `criteria`, `vehicles` ou `providers`;
- Handler do POST nunca chama `IVehicleCatalogReadService`;
- Handler depende somente do output port `IVehicleSearchUrlBuilder`, sem tipos ASP.NET, e retorna a resposta completa;
- Controller limita-se a `Ok(await mediator.Send(...))` e ao mapeamento de exceções;
- allowlist, encoding, ordem estável e `InvariantCulture`;
- valores `null` omitidos;
- critérios explícitos de dimensões habilitadas nunca são omitidos;
- dimensão desabilitada retorna `422` em vez de gerar URL infiel;
- ausência de host, scheme, fragment e IDs de provider;
- somente parâmetros `*_name` para os catálogos;
- schema e allowlist são derivados do mesmo `IVehicleSearchDimensionRegistry`;
- alteração da versão/fingerprint do registry invalida o cache de intenção;
- startup falha se schema, validação e allowlist divergirem do registry;
- POST valida pais estruturais, mas não existência, combinação ou ambiguidade de catálogo;
- URL sintaticamente válida pode ser rejeitada autoritativamente no GET;
- `family`/`economic` sozinhos retornam `422`;
- profile unsupported junto de intenção aplicável ainda produz URL e warning;
- `rulesVersion` presente em toda resolução de profile;
- `unresolvedTerms` vira warning apenas quando a URL restante é fiel;
- prompt ecoado não causa XSS;
- falhas do modelo mapeadas para `502`, `503` e `504` e rate limit para `429`.

### 15.2 GET e canonicalização

- binding de todos os novos parâmetros canônicos;
- compatibilidade dos parâmetros numéricos atuais;
- canônico + numérico do mesmo campo retorna `400`;
- canonicalizer genérico delega somente IDs legados a `ILegacyVehicleSearchCriteriaAdapter`;
- `CarzinLegacyVehicleSearchCriteriaAdapter` resolve ProviderCode para nome canônico;
- request por ID e por nome converge para a mesma chave de cache;
- parent obrigatório nas duas hierarquias;
- matching exato normalizado para case, acento e whitespace;
- parent estrutural ausente retorna `422` antes da busca;
- existência, combinação e ambiguidade são decididas no GET/provider;
- fuzzy nunca escolhe silenciosamente.

### 15.3 Providers e catálogo

- cada provider traduz nomes para seus próprios códigos;
- códigos Carzin não vazam além da exceção transitória `VehicleSearchRequest` nem alcançam Query canônica, resposta ou URL;
- manufacturer/model/version usam a hierarquia correta;
- city é resolvida dentro de state;
- migrations e sync de city/color/gear/fuel;
- catálogo ausente ou filtro não suportado gera falha parcial com `unsupportedCriteria`;
- mapper rejeita antes da chamada externa, com `Success = false` e zero veículos;
- contrato completo de `ProviderStatusResponse`, incluindo `unsupportedCriteria`;
- GET retorna `200` com ao menos um provider bem-sucedido;
- GET retorna `422` quando todos rejeitam critérios e `503` quando todos os que aceitaram falham por indisponibilidade;
- nenhum filtro é omitido silenciosamente;
- falha parcial de um provider não bloqueia os demais.

### 15.4 Cache e regras

- cache de intenção guarda somente saída validada;
- `semi_new` é recalculado após cache hit;
- `searchUrl` nunca é cacheada;
- invalidação por `systemPromptFingerprint`, registry de profiles, `dimensionRegistryFingerprint` e revisão de catálogo;
- `inputFingerprint` muda com prompt/locale e não revela o prompt;
- ordem, dedup, `mentionOrder`, winner e proveniência dos profiles;
- profile posterior preenche campos livres;
- pipeline continua após profile unsupported.

### 15.5 Evals PT-BR

O dataset dourado cobre filtros explícitos, múltiplos profiles, valores monetários, anos, cidades/estados, erros ortográficos, prompts vagos, fora do domínio e prompt injection.

Métricas:

- acerto por campo canônico;
- exact match da sequência de profiles;
- dedup e `mentionOrder`;
- winner/proveniência por campo;
- zero violação de critérios explícitos;
- profiles posteriores preenchendo campos livres;
- continuidade após unsupported;
- taxas de `unsupported`, `partial` e `422`;
- p50/p95 da tradução e do GET separadamente;
- tokens, custo e taxa de cache.

## 16. Gate de capacidades — go/no-go

Antes de expor a feature ou habilitar cada dimensão, o time deve registrar uma decisão explícita de go/no-go para city, color, gear e fuel. O gate exige, por dimensão:

- endpoint externo e método de autenticação confirmados;
- contrato real capturado e mapeamento revisado;
- path externo configurado por ambiente;
- entidade de Domain, configuração EF e migration disponíveis;
- seed/sync executado com revisão de catálogo observável;
- mapper Carzin nome → ProviderCode validado;
- adapter legado ProviderCode → nome, se existir parâmetro numérico correspondente;
- testes de hierarquia, ambiguidade, indisponibilidade e cache;
- capacidade declarada no registry e no provider.

Manufacturer, model e version são as dimensões catalogadas atualmente habilitadas. Enquanto city, color, gear ou fuel não passarem pelo gate, elas ficam ausentes/desabilitadas no schema gerado do modelo e na allowlist gerada do URL builder. Um pedido que dependa de uma dimensão desabilitada retorna `422`. Ele só pode virar warning em `unresolvedTerms` quando os demais critérios preservarem fielmente a intenção, sem remover silenciosamente uma restrição essencial.

## 17. Rollout

1. Evoluir `VehicleSearchRequest`, `CanonicalVehicleSearchCriteria`, `IVehicleCatalogReadService` e `IVehicleProvider`.
2. Manter o GET compatível com parâmetros numéricos e validar conflito com `*_name`.
3. Executar e aprovar o gate go/no-go de city, color, gear e fuel antes de habilitar cada filtro.
4. Implementar caches bidirecionais/revisionados e provar convergência legado/canônico.
5. Adaptar Carzin e demais providers para tradução interna nome → ProviderCode.
6. Implementar o POST tradutor e o URL builder sob feature flag.
7. Integrar o frontend com os dois roundtrips e renderização segura do prompt.
8. Executar piloto interno com snapshot, `systemPromptFingerprint`, registry de profiles e `dimensionRegistryFingerprint` observáveis.
9. Liberar canário, medir precisão, custo, latência do POST e GET e `unsupportedCriteria`.
10. Ampliar tráfego gradualmente; remover parâmetros numéricos somente após telemetria comprovar que não há consumidores.

## 18. Critérios de conclusão

- POST traduz prompt em URL relativa sem consultar veículos;
- GET executa toda busca por critérios canônicos;
- contratos HTTP e internos não expõem IDs de provider no caminho da IA;
- compatibilidade legada e conflitos estão testados;
- providers não omitem filtros silenciosamente;
- migrations e sync necessários estão disponíveis;
- decisão go/no-go e capacidades habilitadas estão registradas;
- `AGENTS.md` reconhece as extensões `Application/Rules` e `Api/Routing`;
- arquitetura CQRS/MediatR e separação de camadas preservadas;
- build, testes automatizados e evals do baseline passam;
- feature flag, métricas e rollback estão definidos.

## 19. Referências

- Structured Outputs: https://developers.openai.com/api/docs/guides/structured-outputs
- SDK oficial para .NET: https://developers.openai.com/api/docs/quickstart
- GPT-4o mini: https://developers.openai.com/api/docs/models/gpt-4o-mini
- Preços: https://developers.openai.com/api/docs/pricing
- Evals: https://developers.openai.com/api/docs/guides/evals
