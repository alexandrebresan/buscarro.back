# AGENTS.md

## Objetivo do projeto

Este projeto é um back-end em .NET para uma Web API responsável por consultar múltiplas APIs externas de veículos, tratar diferenças entre os dados recebidos e retornar uma resposta padronizada para o consumidor da API.

O sistema deve buscar informações em diferentes provedores externos, como OLX, WebMotors e outros serviços semelhantes. Como cada API pode retornar estruturas, nomes de campos e formatos diferentes, o sistema deve normalizar esses dados para um contrato interno consistente e previsível.

O foco principal do projeto é:
- integrar múltiplas APIs externas de veículos;
- tratar diferenças de contrato entre provedores;
- normalizar os dados em DTOs padronizados;
- manter arquitetura limpa, organizada e extensível;
- permitir evolução futura do sistema sem acoplamento excessivo.

---

## Papel do agente neste repositório

Você deve agir como um especialista em .NET, C# e Arquitetura Limpa (Clean Architecture), tomando decisões técnicas consistentes, priorizando boas práticas, separação de responsabilidades, baixo acoplamento e alta coesão.

Ao atuar neste projeto, você deve:
- preservar a arquitetura definida neste documento;
- implementar soluções simples, legíveis e extensíveis;
- evitar acoplamento desnecessário entre integrações;
- garantir separação clara de responsabilidades;
- manter a organização estrutural do projeto;
- corrigir trechos do código que não estejam seguindo este padrão arquitetural.

Sempre que encontrar código fora da arquitetura definida neste arquivo, você deve ajustar a estrutura e refatorar o necessário para adequá-lo ao padrão do projeto.

Exemplo:
- se houver Query e Handler na mesma pasta, você deve separar;
- se houver DTO em pasta incorreta, mover para DTOs;
- se houver regra de negócio em controller, mover para Application.

---

## Arquitetura obrigatória

A arquitetura deve seguir **CQRS com MediatR**.

### Regras obrigatórias
- Commands e Queries separados
- Handlers separados de Commands e Queries
- DTOs em pasta própria
- Controllers finos
- Application contém lógica de uso
- Domain contém núcleo do negócio
- Infrastructure contém integrações e persistência
- Usar Unit of Work quando houver transação

---

## Estrutura de pastas obrigatória

```text
src/
  Buscarro.Api/
    Controllers/
    Configurations/
    Middlewares/

  Buscarro.Application/
    Commands/
    Queries/
    Handlers/
    DTOs/
    Interfaces/

  Buscarro.Domain/
    Entities/
    Enums/
    Interfaces/

  Buscarro.Infrastructure/
    Contracts/
    Providers/
    Repositories/
    Persistence/
    UnitOfWork/
```

---

## Regras por camada

### API

Contém:

* Controllers
* Configuração
* Middlewares

Regras:

* Não conter regra de negócio
* Não acessar APIs externas diretamente
* Apenas chamar MediatR

---

### Application

Contém:

* Commands
* Queries
* Handlers
* DTOs
* Interfaces

Regras:

* Commands ≠ Queries ≠ Handlers
* DTOs separados
* Orquestra fluxo do sistema
* Usa MediatR
* Não depende de HTTP
* Não contém lógica de infraestrutura

---

### Domain

Contém apenas:

* Entities
* Enums
* Interfaces

Regras:

* Não ter DTOs
* Não ter integração externa
* Não ter persistência
* Representar o domínio puro

---

### Infrastructure

Contém:

* Contracts
* Providers
* Repositories
* Persistence
* UnitOfWork

Regras:

* Toda integração externa fica aqui
* DTO externo fica aqui
* Não expor DTO externo na API
* Fazer mapeamento explícito

---

## Padrões obrigatórios

### CQRS

* Queries: leitura
* Commands: escrita

### MediatR

* Toda ação passa por MediatR
* Controllers não chamam lógica diretamente

### Unit of Work

* Usar quando houver persistência com transação

---

## Domínio principal

Entidade principal: **Vehicle**

Exemplos de campos:

* Id externo
* Provider
* Title
* Brand
* Model
* Version
* Year
* Mileage
* Fuel
* Transmission
* Color
* Price
* City
* State
* URL

---

## Integrações externas

Cada API externa pode ter estrutura diferente.

Exemplo:

* name
* car_name
* title

### Regras

* Cada provider é isolado
* Cada provider tem seu DTO
* Não reutilizar DTO entre providers
* Não expor DTO externo
* Mapear explicitamente
* Não inventar dados
* Não copiar dados entre fontes

---

## Normalização

Deve:

* padronizar dados
* tratar campos diferentes
* lidar com ausência de dados

Não deve:

* inventar valores
* copiar dados entre fontes
* compartilhar objetos entre respostas

---

## DTOs

DTO interno:

```
Buscarro.Application/DTOs/
```

DTO externo:

```
Buscarro.Infrastructure/Contracts/
```

Regras:

* não misturar DTO interno com externo
* sempre mapear explicitamente

---

## Convenções

* Command → `Command`
* Query → `Query`
* Handler → `Handler`
* Response → `Response`
* Interface → `I`

---

## Reorganização obrigatória

Se encontrar código fora do padrão, corrigir.

Exemplo errado:

```
Application/Features/Pokemon/
  GetPokemonQuery.cs
  GetPokemonHandler.cs
```

Corrigir para:

```
Application/Queries/GetPokemonQuery.cs
Application/Handlers/GetPokemonHandler.cs
```

---

## Regras de implementação

* Alterar na camada correta
* Não misturar responsabilidades
* Evitar refatoração desnecessária
* Código simples e claro
* Preservar CancellationToken

---

## Tratamento de erros

* Identificar provider
* Log claro
* Não esconder erro
* Não expor detalhes sensíveis

---

## Qualidade de código

Priorizar:

* simplicidade
* clareza
* separação de responsabilidades

Evitar:

* controllers gordos
* handlers grandes
* código duplicado
* acoplamento entre providers

---

## Critérios de conclusão

Uma task só está pronta quando:

* arquitetura respeitada
* pastas organizadas corretamente
* código compila
* alteração mínima necessária
* comportamento correto

---

## Regra final

Sempre seguir este AGENTS.md.

Se encontrar código fora do padrão:
→ corrigir

Se tiver dúvida:
→ manter arquitetura
→ não inventar dados
→ separar responsabilidades
→ manter consistência

## Regra de Execução de Agentes

Toda execução deve passar pelo orquestrador.

Fluxo obrigatório:
1. Architect
2. Implementer
3. Reviewer

Nenhuma implementação pode ser feita sem:
- análise arquitetural prévia
- revisão posterior

O agente deve assumir automaticamente esse fluxo usando:

orchestrator.md