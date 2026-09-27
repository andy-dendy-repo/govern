---
title: Govern knowledge base index
tags: [index, docs]
code: [docs]
updated: 2026-09-27
---

# Govern knowledge base

The project's knowledge base, used for RAG. Every document has front matter (title, tags, code paths, updated date) and covers one topic. The writing rules are in `.claude/rules/docs.md`.

## Architecture
- [architecture.md](architecture.md): layers, allowed references, request flow, folder templates (UF / AGFT / 3DFT), naming rules and how they are enforced.

## Features
- [features/client.md](features/client.md): Blazor WASM client architecture (CFT feature folders), `Api:BaseUrl` configuration, CSP and security headers on the Static Web App, release-time configuration, bUnit and architecture tests.
- [features/users.md](features/users.md): sign-up, sign-in, refresh token rotation and reuse detection, session cap and cleanup, sign-out, current user; endpoints, errors, data model.

## Decisions
- [decisions/0001-mediator.md](decisions/0001-mediator.md): in-house mediator with `Result` responses and error-handling, logging and validation pipelines.
- [decisions/0002-auth-tokens.md](decisions/0002-auth-tokens.md): JWT access tokens, rotating refresh tokens, and client token storage.
- [decisions/0003-no-mapping-libraries.md](decisions/0003-no-mapping-libraries.md): EF projections and static `Projection`/`From` members instead of mapping libraries.

## Operations
- [operations/local-development.md](operations/local-development.md): prerequisites, local Postgres, user-secrets, running the Api, tests and the container.
- [operations/configuration.md](operations/configuration.md): every configuration key, its default and where secrets come from.
- [operations/deployment.md](operations/deployment.md): Azure resources, release workflow, one-time setup, known gaps.
- [operations/dns.md](operations/dns.md): Cloudflare records and Azure custom domain binding.
- [operations/branching.md](operations/branching.md): branches, PR flow, release and protection.

## Plans
- [plans/template-solution-roadmap.md](plans/template-solution-roadmap.md): how the template was built, step by step, with status.
- [plans/follow-ups.md](plans/follow-ups.md): prioritised backlog (least-privilege DB access, monitoring, Blazor client authentication and pages).
- [plans/follow-ups-delivery.md](plans/follow-ups-delivery.md): delivery plans for the follow-ups backlog, one increment per item, in order.
- [plans/blazor-client-delivery.md](plans/blazor-client-delivery.md): remaining delivery plan for the Blazor client (authentication and pages).
- [plans/session-and-email-verification.md](plans/session-and-email-verification.md): remaining increments: sign-up email inside the DB transaction, then email verification.
