# 💎 CrystalReportsAPI (Legacy Bridge)

![.NET Framework 4.8](https://img.shields.io/badge/.NET_Framework-4.8-5C2D91?style=for-the-badge&logo=.net&logoColor=white)
![Crystal Reports](https://img.shields.io/badge/SAP-Crystal_Reports-008FD3?style=for-the-badge&logo=sap&logoColor=white)
![Web API 2](https://img.shields.io/badge/ASP.NET-Web_API-512BD4?style=for-the-badge&logo=microsoft)
![Swagger](https://img.shields.io/badge/Docs-Swagger_UI-85EA2D?style=for-the-badge&logo=swagger&logoColor=black)

**CrystalReportsAPI** is a dedicated microservice designed to bridge the compatibility gap between modern **.NET Core / Mobile Applications** and legacy **SAP Crystal Reports**.

Since modern .NET frameworks (Core/5/6+) do not support the Crystal Reports runtime, this API acts as a secure generation engine, accepting data requests via REST and returning rendered **PDF** or **Excel** files to any client (React, Flutter, .NET Maui, etc.).

---

## 🚀 Key Features

### 🔌 Modern Integration
* **RESTful Architecture:** Exposes legacy reporting logic as standard HTTP endpoints.
* **Cross-Platform Delivery:** Delivers pixel-perfect reports to **Mobile Apps**, **React Dashboards**, and **Linux-hosted** .NET Core services.
* **Format Agnostic:** On-demand export to **PDF** (Portable Doc Format) or **Excel** (.xls).

### 🛡️ Enterprise Security
* **API Key Authentication:** Protected by a custom `X-API-Key` filter with secure, constant-time comparison logic to prevent timing attacks.
* **Database Isolation:** Automatically injects secure SQL credentials at runtime, keeping database connection details hidden from the report definitions (`.rpt` files).

### ⚙️ Advanced Report Handling
* **Dynamic Parameters:** Parses and injects report parameters (`@Test_IDs`, `@Patient_ID`, etc.) dynamically from the JSON request.
* **Sub-Report Support:** Recursively applies connection settings to all sub-reports within a master report.
* **Diagnostic Tools:** Built-in `/diagnose` endpoint to verify Runtime Installation, 64-bit compatibility, and folder permissions in production environments.

---

## 🏗️ Architecture

This API serves as a "Sidecar" service to your main application stack.

```mermaid
graph LR
    A["Mobile App / React Web"] --"POST /generate (JSON)"--> B("CrystalReportsAPI (.NET 4.8)");
    B --"Load .rpt"--> C["Crystal Reports Engine"];
    C --"Fetch Data"--> D[("SQL Server")];
    C --"Render PDF/XLS"--> B;
    B --"Return File Byte[]"--> A;
