# Technical Context

## 1. Solution Topology
The solution follows a **Serverless Microservices** pattern hosted on **Azure Static Web Apps**.

| Project | Namespace | Type | Purpose |
| :--- | :--- | :--- | :--- |
| **Client** | `fmassman.Client` | Blazor WASM (.NET 8) | UI, ViewModels, HTTP Service Facades. |
| **Api** | `fmassman.Api` | Azure Functions (Isolated) | HTTP Triggers, Repository Implementations, OpenAI Integration. |
| **Shared** | `fmassman.Shared` | Class Library (.NET 8) | DTOs, Enums, **Repository Interfaces**, Domain Models. |
| **Tests** | `fmassman.Tests` | xUnit | Unit tests for Shared logic and Api handlers. |

## 2. Connectivity & Data Flow
* **Hosting Model:** Azure Static Web Apps (Standard).
* **Client-Side Pattern:**
    * Do **not** make HTTP calls directly in Razor pages.
    * Create typed services in `fmassman.Client/Services` (e.g., `ApiRosterService.cs`) that implement an interface (e.g., `IRosterService`).
    * Use relative paths for HTTP calls (e.g., `client.GetAsync("api/roster")`).
* **Serialization:** Use `System.Text.Json` exclusively.

## 3. Data Persistence
* **Database:** Azure Cosmos DB (NoSQL).
* **Architecture:**
    * **Interfaces:** Defined in `fmassman.Shared/Interfaces` (e.g., `IPositionRepository`).
    * **Implementations:** Defined in `fmassman.Api/Repositories` (e.g., `CosmosPositionRepository`).
    * **Strict Rule:** `fmassman.Shared` must NOT reference `Microsoft.Azure.Cosmos`. All DB dependencies belong in `Api`.
* **Connection:**
    * **Local:** `local.settings.json` (Key: `CosmosDbConnection`).
    * **Production:** Managed Identity / Environment Variables.

## 4. Development Rules
* **New Features:**
    * **1. Shared:** Define the DTO and Interface in `fmassman.Shared`.
    * **2. Api:** Create the Repository in `fmassman.Api/Repositories` and the Function in `fmassman.Api/Functions`.
    * **3. Client:** Create the Service in `fmassman.Client/Services` and register it in `Program.cs`.
* **Testing:**
    * All complex logic (Analyzers, Calculators) must have unit tests in `fmassman.Tests`.
    * When refactoring, ensure tests referencing concrete repositories (e.g., `CosmosRosterRepository`) are updated to the new `fmassman.Api` namespace.
* **Dependency Injection:** Heavily used. Ensure all new Services and Repositories are registered in their respective `Program.cs`.