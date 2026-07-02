# Operational Design & Scaling Blueprint

This document outlines the production deployment, monitoring, schema management, and scaling strategies required to move the **Secure Notes Vault API** from a local sandbox to a highly available, defense-grade enterprise platform.

---

## 1. Production Deployment Strategy

To align with modern, secure DoD infrastructure, the service will be deployed using a **Cloud-Native, Stateless, and Containerized** architecture pattern on a managed Kubernetes cluster (such as Amazon EKS or Azure AKS).

### Core Components
*   **Containerization:** The API application is packaged into a minimal, hardened container image using a multi-stage `Dockerfile` to strip out unnecessary build tools and keep the attack surface minimal.
*   **Orchestration & Autoscaling:** Deployed using Kubernetes Deployment manifests. A Horizontal Pod Autoscaler (HPA) manages instance counts dynamically based on CPU and Memory usage thresholds.
*   **Ingress & TLS Termination:** An enterprise Ingress Controller terminates public TLS traffic (HTTPS) using strict TLS 1.3 protocols, passing clean traffic downstream to the internal cluster nodes.
*   **Secrets Isolation:** Under a strict Zero-Trust approach, database passwords, JWT signing keys, and master encryption keys are **never** bundled into the repository configuration files. They are stored inside a dedicated key management system (like AWS Secrets Manager) and safely injected into container environment variables at application startup.

---

## 2. Observability: Monitoring & Alerting Matrix

We track three distinct pillars of telemetry to ensure absolute reliability and catch unauthorized access attempts early.

### A. Critical Security & Identity Alerts (Immediate Notification)
*   **Spike in HTTP 403 (Forbidden) Responses:** A sudden surge in 403 codes signals a horizontal privilege-escalation attack or a malicious user attempting to access notes they do not own.
*   **Cryptographic Decryption Failures:** If the `AesGcmEncryptionService` throws a decryption integrity exception, it means a database row has been altered or corrupted directly at the storage disk level.
*   **High-Volume Failed Logins (HTTP 401):** Tracks sudden brute-force credential stuffing attacks against the `/api/auth/login` gateway.

### B. Golden Infrastructure Metrics
*   **API Latency:** Alert if the 95th percentile (P95) request execution duration climbs beyond 200ms.
*   **Database Connection Pool Exhaustion:** Tracks if the API instance connection limits are maxing out, causing slow or dropped database requests.
*   **Container CPU/Memory Throttle limits:** Notifies infrastructure teams if pods are operating near their hardware capacities before a crash occurs.

### C. Audit Logging Strategy
All structural endpoints use structured logging (via tools like Serilog) to ship plaintext event logs to a centralized storage cluster (like Splunk or an ELK stack). **Strict Rule:** Note content strings and raw user passwords must be completely filtered out of all logging mechanisms to satisfy strict privacy and compliance requirements.

---

## 3. Database Migrations Over Time

Running `dotnet ef database update` directly against a live production database instance during a deployment is an anti-pattern. It risks database locking, timeouts, and application downtime.

### The Production Migration Pipeline
1.  **Generation:** Developers run the Entity Framework tool locally to generate code-based migrations.
2.  **SQL Script Extraction:** The CI/CD pipeline runs a dry-run script generation command:
    ```bash
    dotnet ef migrations script --output migration.sql --idempotent
    ```
    This outputs a pure, idempotent raw SQL script that verifies whether specific changes have already been applied before executing them.
3.  **Governance & Execution:** The generated SQL script is reviewed by a Database Administrator (DBA) and run against the production database cluster using a dedicated database management deployment step right *before* the new API containers go live.

### Expanding the Schema with Zero Downtime
To upgrade the application without taking the site offline, all changes must follow an **Expand and Contract** design pattern. 

For example, if you need to rename a table column:
*   **Release 1 (Expand):** Add the brand-new column to the database via migration while keeping the old column active. Update the API code to write to *both* columns but read only from the old one.
*   **Release 2 (Data Backfill):** Run a background database script to safely copy data from the old column over to the new column.
*   **Release 3 (Transition):** Update the API code to read and write entirely from the new column.
*   **Release 4 (Contract):** Run a final migration to safely drop the old column from the database once stability is fully verified.

---

## 4. Scaling to 10,000 Concurrent Users

Supporting 10,000 concurrent connections shifts the application bottleneck away from our stateless web api code and directly onto the **shared database connections and cryptographic compute costs**.

To scale the architecture safely, we implement three major changes:

### A. Introduce Distributed Caching (Redis Cluster)
*   **The Problem:** Decrypting note rows on every single page load uses a significant amount of CPU power, and reading from MySQL on every request will quickly exhaust database connection pools.
*   **The Solution:** Add a **Redis Distributed Cache** cluster. When a user requests a note, the API checks Redis first. If it's a cache hit, it serves the note instantly.
*   **Cache Eviction Rule:** Whenever a user hits a `PUT` or `DELETE` endpoint, the API executes a cache eviction command to wipe that specific note ID out of Redis globally, ensuring data consistency.

### B. Implement Database Read/Write Splitting
*   **The Strategy:** Set up a MySQL replica configuration consisting of one **Primary (Write) Node** and multiple distributed **Read Replicas**.
*   **The API Optimization:** Update the `NotesController.cs` endpoints. Creation, update, and deletion tasks route directly to the Primary Node. Read operations (such as listing notes or fetching a note by its identifier) are optimized using EF Core's `.AsNoTracking()` function and automatically routed to the lightweight Read Replicas to spread out the transaction load.

### C. Optimize JWT Cryptographic Validation Overhead
*   **The Optimization:** Validating token signatures on every single HTTP incoming packet is computationally expensive at massive scale. We switch the JWT signature validation engine to utilize asymmetric keys (**RS256** or **ES256** public/private key pairs) and cache the verified key profiles inside memory buffers on each API node, reducing CPU usage across the cluster.
