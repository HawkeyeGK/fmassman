# FM26-Helper Architecture

## Overview
FM26-Helper is a .NET 8 tool suite designed to analyze Football Manager 2026 player data. It extracts data from screenshots, calculates derived attributes and role suitability, and visualizes the roster via a Web Dashboard.

## Solution Structure

### 1. fmassman.Shared (Class Library)
**Purpose:** The Core Logic and Data Models.
* **Models:** `PlayerImportData` (Identity), `PlayerSnapshot` (Attributes).
* **Services:**
    * `CosmosRosterRepository`: Handles data persistence via Azure Cosmos DB.
    * `PlayerAnalyzer`: The main entry point for calculations. Returns `PlayerAnalysis`.
    * `RoleFitCalculator`: Reads `roles.json` and calculates 0-100% suitability scores based on weighted attributes (Primary 3x, Secondary 2x).
* **Data:** `roles.json` (Source of truth for Role definitions).

### 2. fmassman.Api (Azure Functions)
**Purpose:** Backend Logic, Data Persistence, and Data Ingestion.
* **Workflows:**
    * **API Endpoints:** RESTful endpoints for the Client to access Roster data (`RosterFunctions`), Miro integration (`MiroBoardFunctions`), and more.
    * **Data Extraction:** Reads `.png` screenshots -> Slices Image -> GPT-4o API -> Maps to `PlayerImportData` -> Saves to DB. (Managed by `ImageProcessor`).
* **Key Functions:**
    * `RosterFunctions`: CRUD operations for players.
    * `MiroBoardFunctions`: Integration with Miro API.
    * `ImageProcessor`: Orchestrates the image extraction pipeline.

### 3. fmassman.Client (Blazor Web App)
**Purpose:** Visualization & Decision Support.
* **Pattern:** ViewModel.
    * `RosterItemViewModel`: Flattens raw data + analysis for the grid.
* **Pages:**
    * `Home.razor`: Displays the Roster Grid (Name, Bio, Derived Attributes, Best Roles).
    * `PlayerUpload.razor`: Interface for uploading screenshots.

## Key Workflows
1.  **Analysis:** Raw Stats -> `PlayerAnalyzer` -> `PlayerAnalysis` (Speed, DNA, Role Scores).
2.  **Role Engine:** Roles are defined in `roles.json`. The engine dynamically calculates fit using Reflection to match JSON keys to C# properties.

## Tech Stack
* .NET 8 (C#)
* Blazor WebAssembly
* Azure Functions
* Azure Cosmos DB
* OpenAI GPT-4o (Vision)
* System.Text.Json