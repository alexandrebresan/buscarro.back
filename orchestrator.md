# Orquestracao obrigatoria

Toda evolucao deste repositorio passa, nesta ordem, pelos papeis **Architect**, **Implementer** e **Reviewer**.

## 1. Architect

- Confirma escopo, contratos, camadas e dependencias antes de qualquer implementacao.
- Verifica aderencia a Clean Architecture, CQRS com MediatR e separacao entre DTOs internos e contratos externos.
- Define testes de aceitacao, riscos e limites da fase.
- Entrega ao Implementer uma especificacao decision-complete.

## 2. Implementer

- Trabalha somente no escopo aprovado e preserva `CancellationToken` e fluxos legados requeridos.
- Usa TDD: cria ou ajusta o teste, observa o RED pelo motivo esperado, implementa o minimo necessario e confirma o GREEN.
- Executa build e testes proporcionais ao risco e registra comandos e resultados.
- Nao avanca sobre pendencias arquiteturais criticas nem mascara falhas de provider ou configuracao.

## 3. Reviewer

- Revisa diff, arquitetura, seguranca, compatibilidade e evidencias de teste de forma independente.
- Classifica achados; qualquer pendencia critica bloqueia a conclusao e retorna ao Implementer.
- Exige nova verificacao completa depois das correcoes antes de aprovar a fase.

## Gates operacionais

- Nenhuma implementacao comeca sem o gate do Architect.
- Nenhuma fase e considerada concluida sem build limpo, testes aplicaveis verdes e aprovacao do Reviewer.
- Segredos nunca entram em arquivos versionados; configuracoes sensiveis usam ambiente ou secret store.
- Commit, merge, push e abertura de pull request sao proibidos sem autorizacao explicita do responsavel pelo repositorio.
