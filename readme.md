# Secure Notes Vault API (.NET 8.0 Core)

A defense-grade, production-ready REST API implementing an encrypted data vault ecosystem for managing and sharing sensitive textual assets. Built under a Zero-Trust architecture model to enforce rigorous access isolation, explicit cryptographic protection, and automated compliance testing.

---

## 1. System Overview & Architecture

This application is designed following **Clean Architecture** and **Separation of Concerns (SoC)** paradigms. The system boundaries are strictly partitioned into three decoupled projects to prevent framework leakages and guarantee high testability:

## 2. Tech Choices

### Language & Runtime: C# and .NET 8.0 CoreCross-Platform Performance: Runs with high efficiency inside lightweight cloud containers like Docker.Enterprise Architecture: Provides a robust, built-in Dependency Injection (DI) system and structured middleware pipeline out of the box.Ecosystem Agnostic: The structural patterns used here (like controllers, services, and repositories) translate directly 1:1 to Java Spring Boot or Python FastAPI.

### Storage Layer: MySQL (Relational Database)Data Integrity: Enforces strict ACID compliance to guarantee that notes and access controls are modified reliably.Hardened Relations: Enforces relational boundaries using foreign keys and unique indexes directly at the database engine level. This blocks data leaks or duplicate data rows that code-level checks might miss.

### Data Access Layer: Entity Framework Core (EF Core)Automated SQL Injection Protection: Natively uses parameterized queries for all operations. This entirely blocks SQL injection attacks by design.Database Version Control: Uses Code-First migrations to track and version database changes straight inside the source code repository.

### Assertion Engine: xUnit, Moq, & EF Core In-Memory ProviderIsolated Integration Tests: The In-Memory provider spins up a temporary database inside the application's RAM. This allows the test suite to verify complex database queries and relationship limits instantly without needing a real MySQL server running.CI/CD Ready: Tests run independently without any local hardware dependencies, making them perfect for automated deployment pipelines.


## 3. API documentation

SecureNotesVault/
├── SecureNotesVault.sln                   ⚠️ **Authentication Requirement:** All `/api/notes` routes require a header configuration of `Authorization: Bearer <TOKEN_STRING>` utilizing the token generated from a successful login endpoint path.

### A. User Registration
*   **Endpoint:** `POST /api/auth/register`
*   **Payload:**
    ```json
    { "username": "clark_kent", "password": "SuperSecurePassword123!" }
    ```
*   **Response (201 Created):**
    ```json
    { "message": "User registered successfully.", "userId": 1 }
    ```

### B. User Login (Obtaining JWT Session Token)
*   **Endpoint:** `POST /api/auth/login`
*   **Payload:**
    ```json
    { "username": "clark_kent", "password": "SuperSecurePassword123!" }
    ```
*   **Response (200 OK):**
    ```json
    { "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." }
    ```

### C. Create an Encrypted Note
*   **Endpoint:** `POST /api/notes`
*   **Payload:**
    ```json
    { "content": "The launch coordinates are 38.8977° N, 77.0365° W." }
    ```
*   **Response (201 Created):**
    ```json
    { "message": "Note secured successfully.", "noteId": 5 }
    ```

### D. List Accessible Notes
*   **Endpoint:** `GET /api/notes`
*   **Response (200 OK):** *(Displays all notes owned by or shared with the caller, seamlessly decrypted in memory)*
    ```json
    [
      {
        "id": 5,
        "ownerId": 1,
        "content": "The launch coordinates are 38.8977° N, 77.0365° W.",
        "createdAt": "2026-07-02T19:45:00Z",
        "updatedAt": "2026-07-02T19:45:00Z",
        "isReadOnly": false
      }
    ]
    ```

### E. Get a Specific Note By ID
*   **Endpoint:** `GET /api/notes/5`
*   **Response (200 OK):**
    ```json
    {
      "id": 5,
      "ownerId": 1,
      "content": "The launch coordinates are 38.8977° N, 77.0365° W.",
      "createdAt": "2026-07-02T19:45:00Z",
      "updatedAt": "2026-07-02T19:45:00Z",
      "isReadOnly": false
    }
    ```
*   *Note: If a different unauthenticated hacker user requests this ID path, the API automatically triggers a strict `403 Forbidden` response block.*

### F. Update an Existing Note
*   **Endpoint:** `PUT /api/notes/5`
*   **Payload:**
    ```json
    { "content": "The updated launch coordinates are 39.0000° N, 78.0000° W." }
    ```
*   **Response (200 OK):**
    ```json
    { "message": "Note updated and re-encrypted successfully." }
    ```
*   *Enforcement Note: If a recipient of a shared note attempts to call this route, the system rejects it with a `403 Forbidden` flag to maintain read-only permissions.*

### G. Share Note Access (Read-Only to Recipient)
*   **Endpoint:** `POST /api/notes/5/share`
*   **Payload:** *(Shares access with user identity 2)*
    ```json
    { "sharedWithUserId": 2 }
    ```
*   **Response (200 OK):**
    ```json
    { "message": "Note access permissions updated successfully." }
    ```

### H. Delete a Note
*   **Endpoint:** `DELETE /api/notes/5`
*   **Response (200 OK):**
    ```json
    { "message": "Note permanently purged from the vault." }
    ```
*   *Relational Integrity: Under the hood, deleting a note triggers an automated database cascading purge, sweeping any associated shared tracking configuration entries out of the tables simultaneously.*


## 4. Assumptions, Trade-offs, & Future Improvements

### Core Assumptions
1.  **Stateless Infrastructure Model:** We assume the API application instances will run in a stateless configuration (e.g., within a container orchestration engine like Kubernetes). Hence, we rely entirely on the database and external infrastructure configurations as the central source of truth.
2.  **Trusted Config Management:** We assume that secrets management (such as the base64-encoded `MasterEncryptionKey` and the JWT `Secret`) will be handled via protected environment injection or toolsets like AWS Secrets Manager/HashiCorp Vault in real deployment stages, rather than being modified inside raw codebase app settings files.

### Architectural Trade-offs
1.  **EF Core Translation Latency:** Utilizing an ORM introduces microsecond compilation parsing overhead compared to writing bare, manual ADO.NET query commands. This choice was deliberately accepted because the trade-off yields significant velocity gains and automatically guarantees absolute parameter protection against SQL injection hazards.
2.  **Single Master Encryption Key:** To fit within evaluation code delivery scopes, the current AES engine utilizes a single global master application key. In a full production defense ecosystem, this introduces an organizational risk vector. 

### Future Enterprise Enhancements
1. Envelope Encryption / Key-Per-User Pattern: Upgrade the cryptographic module to implement envelope encryption. Under this paradigm, every individual user account or single note has its own unique Data Encryption Key (DEK), which is itself encrypted by a Master Key Encryption Key (KEK) housed securely inside a hardware security module (HSM) or KMS environment.

2. Optimistic Concurrency Controls: Introduce tracking version timestamp arrays or Guid concurrency tokens to database entity rows. This handles edge cases in multiple concurrent application environments by throwing explicit exceptions if two separate system requests try to write modifications to the exact same note row concurrently, allowing the API layer to return a clean 409 Conflict response structure.

3. Distributed Caching & Eviction: Integrate a distributed memory engine layer via Redis to eliminate redundant decryption overhead and database reading roundtrips on heavy search endpoints, combined with an automated transaction-driven cache eviction filter to wipe cached note assets whenever updates or schema deletions are triggered.


## 5. Local Development Setup & How to Run

###  The One-Command Docker Setup 
The application is pre-configured to detect its host runtime environment. By running it inside Docker without editing configurations, the API will automatically instantiate an embedded, isolated SQLite vault file inside the container and auto-scaffold all data boundaries on startup.

From the root directory containing the `Dockerfile`, execute:
```bash
docker build -t secure-notes-vault . &&  docker run -p 5000:8080 --name vault-api  secure-notes-vault
```
The secure API vault will boot instantly and begin listening for JSON traffic locally at: `http://localhost:5000`.
Swagger documentation can be viewed at 
`http://localhost:5000/swagger`

