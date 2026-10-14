# Security policy

## Supported versions

Govern is released continuously: every merge to `main` is deployed. Only the latest `main` gets security fixes.

## Reporting a vulnerability

**Please don't report security vulnerabilities through public issues, discussions or pull requests.**

Report them privately through GitHub instead:

1. Open the repo's [**Security** tab](https://github.com/andy-dendy-repo/govern/security).
2. Click **Report a vulnerability**. You can also use the [direct link](https://github.com/andy-dendy-repo/govern/security/advisories/new).
3. Describe the issue and include:
   - the affected component (API endpoint, Blazor client, infrastructure or CI workflow);
   - steps to reproduce, or a proof of concept;
   - the impact you expect (for example, reading another player's hidden game state, or account takeover).

## What to expect

- **Acknowledgement** within 7 days. This is a one-person project, so please allow some time.
- An initial assessment and next steps after that, and updates as the fix progresses.
- Credit in the advisory when the fix is published, unless you prefer to stay anonymous.

## Scope

**In scope:**
- the code in this repository: the API, the Blazor client, the game engine, the Bicep infrastructure and the GitHub Actions workflows;
- authentication and session handling (JWT access tokens and refresh token rotation);
- fog-of-war leaks: any way for a player to learn hidden game state;
- secrets or personal data exposed through the code, configuration or history.

**Out of scope:**
- denial-of-service attacks and volume testing against any deployed instance;
- social engineering;
- findings in third-party dependencies that are already publicly known. Dependabot and the CI vulnerability scan track these, but a report showing a real exploit path in Govern is welcome.

Please test only against your own local instance (see [local development](docs/operations/local-development.md)), never against a deployed instance or other people's accounts.
