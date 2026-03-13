## A. Executive summary

A cloud‑hosted multi‑tenant web app that lets authenticated Microsoft 365 admins run many of the listed DLP/Purview/EXO cmdlets via HTTPS is **technically feasible**, but only by relying heavily on **undocumented Exchange / compliance “Admin API” proxy endpoints** that Microsoft explicitly does not position as a general‑purpose management surface.[^1_1][^1_2][^1_3]

Building a production SaaS that calls `/adminapi/.../InvokeCommand` directly for arbitrary cmdlets is therefore **functionally possible but support‑fragile**, and some cmdlets on your list are **retired in Exchange Online and cannot be reproduced**.[^1_4][^1_5][^1_6]

At a high level:

- **Feasible and relatively safe (if you accept PowerShell in the backend)**
    - All Security \& Compliance / Purview DLP management cmdlets (Get/New/Set/Remove‑DlpCompliancePolicy/Rule, DLP SIT \& keyword dictionary \& fingerprint cmdlets, etc.) are available and supported in **Security \& Compliance PowerShell** via the ExchangeOnlineManagement module’s REST mode.[^1_7][^1_8][^1_9]
    - You can expose them in a web app by orchestrating **structured, allow‑listed PowerShell commands in a container** rather than calling the proxy Admin API directly. This stays on supported ground.
- **Feasible but unsupported / fragile via direct admin proxies**
    - Most classic EXO cmdlets (including Get‑MailDetailDlpPolicyReport, Test‑Message, Test‑TextExtraction, Test‑DataClassification) are proxied over HTTPS to `https://outlook.office365.com/adminapi/.../InvokeCommand` behind the module.[^1_10][^1_11][^1_1]
    - Security \& Compliance / Purview cmdlets (Get‑DlpCompliancePolicy, New‑DlpSensitiveInformationType, New‑DlpFingerprint, etc.) are similarly proxied via `https://<region>.ps.compliance.protection.outlook.com/adminapi/.../InvokeCommand` using a different token audience.[^1_12][^1_3]
    - Calling these endpoints yourself is not documented or supported by Microsoft, and their behavior and availability may change without notice.[^1_2][^1_3][^1_1]
- **Not realistically implementable in Exchange Online**
    - `Export-DlpPolicyCollection` and `Import-DlpPolicyCollection` are explicitly **retired from the cloud‑based service** and are now functional only in on‑prem Exchange; analogous XML import/export workflows are no longer supported in EXO.[^1_13][^1_6][^1_4]
    - Any cmdlets that only ever existed in on‑premises Exchange, or were retired from EXO, are **out of scope** for a Microsoft 365‑only SaaS.
- **Delegated vs app‑only**
    - The **official Exchange Online Admin API** (the new v2.0 surface) supports both delegated and app‑only with `Exchange.ManageV2` / `Exchange.ManageAsAppV2` and is explicitly constrained to a small set of endpoints (OrganizationConfig, Mailbox, MailboxFolderPermission, etc.).[^1_14][^1_2]
    - The broader undocumented `/adminapi/beta/.../InvokeCommand` surface used by the modules supports **delegated and app‑only** when accessed via the modules, but direct app‑only use for SCC / eDiscovery scenarios is explicitly limited (e.g., –EnableSearchOnlySession / dataservice endpoint does *not* support client‑credentials today).[^1_15][^1_16][^1_12]

**Verdict:**

- A **multi‑tenant web UX that drives supported PowerShell sessions in a backend container** is a **yes (go)** for most listed cmdlets.
- A **generic admin‑API proxy SaaS that calls `/InvokeCommand` directly for arbitrary DLP / Purview cmdlets is technically feasible but high‑risk** and best constrained to **read‑only and test‑style operations**, with clear “unsupported” disclaimers.
- Some legacy DLP collection import/export patterns are **no‑go** in EXO and must be redesigned around current Purview capabilities.

***

## B. Technical findings from the source URLs

### 1. How EXO cmdlets are proxied over HTTPS

With ExchangeOnlineManagement v2+, Microsoft rewired many classic EXO cmdlets so they **no longer use WinRM / Remote PowerShell sessions at all**.[^1_17][^1_10]

- On connect, the module still downloads a tmp module (e.g. `tmpEXO_xxxxx.psm1`), but the cmdlet bodies now funnel into an internal function (`Execute-Command` / similar) that issues an **HTTPS POST to an Admin API endpoint** with a JSON payload and OAuth bearer token.[^1_10]
- That endpoint is `https://outlook.office365.com/adminapi/beta/<tenant-id-or-domain>/InvokeCommand` (or `outlook.office.com`), with a body of the form:[^1_1][^1_10]

```json
{
  "CmdletInput": {
    "CmdletName": "Get-Mailbox",
    "Parameters": {
      "Identity": "user@contoso.com"
    }
  }
}
```

- Required headers include:
    - `Authorization: Bearer <access token>` for resource `https://outlook.office365.com`.[^1_11][^1_10]
    - `X-ResponseFormat: json` or `clixml` (the module generally uses CliXML).[^1_10]
    - `X-AnchorMailbox`, usually `UPN:<user@domain>` or a system mailbox in app‑only scenarios, to route the request to the correct backend.[^1_3][^1_14][^1_10]

Michev’s trace shows that **any “classic” Exchange Online cmdlet that now runs successfully via the V2+ module is being proxied in this way**, with batching and pagination done in the module and surfaced by OData constructs like `@odata.nextLink`.[^1_1][^1_10]

### 2. How the InvokeCommand pattern works

The **generic InvokeCommand action** is exposed as one of the “Actions” in the Admin API metadata document at `/adminapi/beta/<tenant>/$metadata`.[^1_3]

- It’s declared as an action named `InvokeCommand` that accepts a `Parameter` payload; the module sends the `CmdletInput` envelope inside that parameter.[^1_3]
- Third‑party code can call `POST https://outlook.office365.com/adminapi/beta/<tenant>/InvokeCommand` directly with the same headers and body the module uses; Michev’s article walks through this and shows the raw JSON and required headers.[^1_1][^1_10]
- The same pattern supports **application permissions** (app‑only) when the token has `Exchange.ManageAsApp` / `Exchange.ManageAsAppV2` roles and the service principal is given suitable Exchange RBAC roles.[^1_18][^1_19][^1_1]

For Security \& Compliance, the **same InvokeCommand pattern exists** but under the **compliance endpoint and resource**:

- Resource / audience: `https://ps.compliance.protection.outlook.com` (REST mode for SCC / Purview PowerShell).[^1_3]

```
- Endpoint: `https://<region>.ps.compliance.protection.outlook.com/adminapi/beta/<tenant>/InvokeCommand` using an access token scoped to `https://ps.compliance.protection.outlook.com/.default` and the same `CmdletInput` envelope.[^1_3]
```

Michev shows `Get-RetentionCompliancePolicy` invoked via this SCC Admin API path using the same header and body shape.[^1_3]

### 3. Exchange Admin API vs SCC / Compliance endpoints

There are now **two conceptually distinct things**:

1. **Exchange Online Admin API (official, v2.0)**
    - Documented on Microsoft Learn as a **small, cmdlet‑style REST surface designed specifically to replace certain EWS admin scenarios**: org config, accepted domains, mailbox folder permissions, Send on Behalf, and distribution group membership.[^1_2]
    - Uses endpoints like `POST https://outlook.office365.com/adminapi/v2.0/<TenantID>/MailboxFolderPermission` with a `CmdletInput` body that only allows a small, documented set of cmdlets and parameters for that endpoint.[^1_14][^1_2]
    - Uses **`Exchange.ManageV2` (delegated)** and **`Exchange.ManageAsAppV2` (app‑only)** permissions and standard OAuth flows.[^1_19][^1_14]
    - Microsoft explicitly states this API **“doesn't replace the full Exchange administration surface in Exchange Online PowerShell”**; it is a focused EWS migration aid.[^1_2]
2. **Undocumented “Admin API” used by PowerShell (beta/InvokeCommand)**
    - The V2+/V3 Exchange module uses `/adminapi/beta/.../InvokeCommand` with the `CmdletInput` pattern to proxy **hundreds of classic cmdlets**, not just those in the official Admin API.[^1_10][^1_1][^1_3]
    - The compliance endpoint (`ps.compliance.protection.outlook.com`) exposes a matching `/adminapi/beta/.../InvokeCommand` surface used by Connect‑IPPSSession REST mode for Purview / SCC cmdlets.[^1_8][^1_9][^1_3]
    - These beta endpoints are **not documented as a general admin API**, and Microsoft has not committed to stability; Michev explicitly calls them “undocumented and unsupported”.[^1_1][^1_3]

For **SCC / Purview**, there is no public equivalent of the EXO v2.0 Admin API; all REST behavior is via **PowerShell‑driven proxies** under the compliance endpoint.[^1_9][^1_8][^1_3]

### 4. Token audiences and resources

From the sources:

- **Exchange Online (EXO) REST / Admin API**
    - Resource / audience: `https://outlook.office365.com`.[^1_11][^1_10]
    - Older traces show scopes like `https://outlook.office365.com/AdminApi.AccessAsUser.All` plus `.default`.[^1_11]
    - The new v2.0 Admin API docs consolidate this into `Exchange.ManageV2` / `Exchange.ManageAsAppV2` permissions under the “Office 365 Exchange Online” resource.[^1_19][^1_14]
- **Security \& Compliance / Purview compliance PowerShell**
    - Standard REST mode resource: `https://ps.compliance.protection.outlook.com` (`.default` scope).[^1_3]

```
- Endpoint `https://<region>.ps.compliance.protection.outlook.com/adminapi/beta/<tenant>/InvokeCommand` used by SCC cmdlets like `Get-RetentionCompliancePolicy` and `Get-ComplianceSecurityFilter` when connected via Connect‑IPPSSession with a REST‑capable module.[^1_20][^1_9][^1_3]
```

- **EnableSearchOnlySession / eDiscovery search endpoint**
    - Connect‑IPPSSession’s `‑EnableSearchOnlySession` switch requests a **different resource/audience**: `https://dataservice.o365filtering.com` with scopes like `https://dataservice.o365filtering.com/AdminApi.AccessAsUser.All` and `.default`.[^1_12]
    - This is used for eDiscovery cmdlets listed in MC1131771 and later expanded lists; the same legacy client id (`fb78d390-0c51-40cd-8e17-fdbfab77341b`) is used, but with the new resource.[^1_21][^1_15][^1_12]

This implies that:

- For **DLP / classification cmdlets that are *not* eDiscovery search‑only**, the relevant REST proxy is under the **`ps.compliance.protection.outlook.com`** audience.
- For **new eDiscovery search‑only cmdlets**, a **different audience** (`dataservice.o365filtering.com`) is required and app‑only / client‑credentials flows are explicitly unsupported.[^1_15][^1_12]


### 5. Connect‑IPPSSession and EnableSearchOnlySession

Connect‑IPPSSession:

- Is the supported entry point for **Security \& Compliance / Purview compliance PowerShell**, and as of ExchangeOnlineManagement v3.2+ it uses **REST API mode instead of WinRM/RPS for virtually all SCC cmdlets**.[^1_8][^1_20][^1_9]
- The `‑EnableSearchOnlySession` switch:
    - Does **not** change the cmdlet list initially; instead, it changes only the **token resource** from `ps.compliance.protection.outlook.com` to `dataservice.o365filtering.com`, as confirmed by Fiddler traces.[^1_12]
    - Microsoft has announced that some eDiscovery cmdlets now **require** this switch (and thus the `dataservice` audience) to run successfully.[^1_21][^1_15][^1_12]

Crucially:

- Microsoft’s message center notice and Michev’s analysis note that these eDiscovery cmdlets **remain incompatible with certificate‑based / app‑only authentication**; they require a delegated user context, and client‑credentials tokens for `dataservice.o365filtering.com` lack the required claims (`scp`, `wids`) and fail with 401.[^1_15][^1_12]


### 6. Can the same proxy approach be used for Purview/DLP cmdlets?

Evidence:

- The official docs classify almost all DLP / SIT / DLP reporting cmdlets on your list as **“available only in Security \& Compliance PowerShell”**.[^1_22][^1_23][^1_24][^1_25][^1_26][^1_27][^1_28][^1_29][^1_30][^1_31]
- Security \& Compliance PowerShell itself is implemented via **Connect‑IPPSSession + ExchangeOnlineManagement v3**, which now uses **REST admin proxy endpoints instead of WinRM**.[^1_20][^1_9][^1_8]

```
- Michev’s article explicitly demonstrates using `https://<region>.ps.compliance.protection.outlook.com/adminapi/beta/<tenant>/InvokeCommand` with `CmdletName="Get-RetentionCompliancePolicy"` and an SCC‑scoped token to run Purview retention cmdlets without PowerShell.[^1_3]
```

Taken together, it is **highly likely** (though not formally documented) that:

- Any SCC‑only Purview cmdlets in your list (e.g., `Get-DlpCompliancePolicy`, `New-DlpComplianceRule`, `Get-DlpSensitiveInformationType`, `New-DlpFingerprint`, `New-DlpKeywordDictionary`, etc.) are backed by the **same InvokeCommand proxy on the compliance endpoint** and can be invoked via that endpoint by mimicking the module’s requests.
- Microsoft, however, **does not document or support** direct use of this InvokeCommand surface; they support using the **ExchangeOnlineManagement module** (and Connect‑IPPSSession) to run those cmdlets.[^1_9][^1_8][^1_1][^1_3]


### 7. Evidence of alternate modern APIs

Beyond the generic `/beta/.../InvokeCommand`:

- The **v2.0 Admin API** introduces **discrete REST endpoints** (`OrganizationConfig`, `Mailbox`, `MailboxFolderPermission`, `DistributionGroupMember`, etc.) that each accept a restricted set of cmdlets \& parameters via a `CmdletInput` body and are documented.[^1_14][^1_2]
- The metadata for `/adminapi/beta/.../$metadata` exposes additional **Actions and Functions that do not have direct PowerShell cmdlets**, like `GetMailDetailTransportRuleReport`, which can be invoked via POST to `/adminapi/beta/<tenant>/GetMailDetailTransportRuleReport` with a custom `QueryTable` JSON payload.[^1_3]

However:

- There is **no similar documented REST surface for Purview DLP configuration or test operations**; docs direct you to **PowerShell (Connect‑IPPSSession + DLP cmdlets)** or the Purview portal UI.[^1_32][^1_23][^1_24][^1_33][^1_7]
- Test cmdlets like `Test-TextExtraction`, `Test-DataClassification`, `Test-DlpPolicies`, and `Test-Message` remain **PowerShell entry points only**, with no published HTTP equivalent.[^1_34][^1_35][^1_36][^1_32]

So while there are **some** “modern” HTTP endpoints beyond InvokeCommand, none currently cover the Purview DLP test surface; those test behaviors are still accessible only via cmdlets (proxied or not).

***

## C. Feasibility matrix (per cmdlet)

**Legend / assumptions**

- **Service plane**
    - *Exchange Admin API*: Exchange Online `/adminapi/...` surface (`outlook.office365.com`)
    - *SCC Admin API*: Security \& Compliance / Purview compliance `/adminapi/...` (`ps.compliance.protection.outlook.com`)
    - *Other*: On‑prem‑only or non‑Admin API.
- **Delegated token required?** — for direct HTTP usage (not via module).
- **App‑only possible?** — in principle, via application permissions + RBAC.
- “Observed proxyable via InvokeCommand” — direct evidence vs inference:
    - *Yes*: explicitly shown for SCC/EXO cmdlets of that type, or covered by Michev for that plane.
    - *Unknown*: not directly mentioned, but likely given general pattern.

I’ll group some rationale in notes to keep the table readable.

```markdown
| Cmdlet                                  | Service plane           | Delegated token required? | App-only possible? | Supported/doc’d by Microsoft? | Observed proxyable via InvokeCommand? | Safe in public multi-tenant app? | Notes / risks / blockers |
|-----------------------------------------|-------------------------|---------------------------|--------------------|-------------------------------|--------------------------------------|----------------------------------|--------------------------|
| Export-DlpPolicyCollection              | Other (on-prem only)   | N/A                       | N/A                | No (retired in EXO)[web:36][web:118] | No (cloud cmdlet removed)          | No                               | Retired from cloud; only on-premises Exchange; cannot call in M365.[web:36][web:121] |
| Import-DlpPolicyCollection              | Other (on-prem only)   | N/A                       | N/A                | No (retired in EXO)[web:103][web:121] | No                                 | No                               | Same as above; transport-rule-based DLP collection import not available in EXO anymore. |
| Get-CustomDlpEmailTemplates             | SCC Admin API (inferred) | No (for HTTP; delegated OR app-only possible) | Probably (via EXO CBA + SCC app perms)[web:98][web:64] | Yes (SCC cmdlet) | Unknown                           | Yes, with restrictions            | SCC-only cmdlet; REST-proxied via Connect-IPPSSession; direct InvokeCommand use unsupported but technically likely. |
| Get-DlpCompliancePolicy                 | SCC Admin API          | No                        | Yes (via Exchange.ManageAsApp + Compliance roles)[web:98][web:64][web:71] | Yes (SCC PowerShell)[web:32][web:76] | Yes (Michev shows Retention* on same plane)[web:10] | Yes, with restrictions            | Good candidate for structured wrapper; REST proxy is undocumented but pattern well understood. |
| Get-DlpComplianceRule                   | SCC Admin API          | No                        | Yes (as above)     | Yes (SCC PowerShell)[web:52]  | Inferred yes                        | Yes, with restrictions            | Same feasibility as Get-DlpCompliancePolicy. |
| Get-DlpKeywordDictionary                | SCC Admin API          | No                        | Yes                | Yes (SCC PowerShell)[web:105][web:111] | Inferred yes                        | Yes, with restrictions            | Operates purely on metadata; low risk if constrained. |
| Get-DlpPolicyTemplate                   | SCC Admin API (inferred) | No                      | Yes                | Yes (SCC PowerShell; templates now mostly managed via Purview portal)[web:38] | Unknown                           | Yes, with restrictions            | APIs are opaque; rely on PowerShell semantics. |
| Get-DlpSensitiveInformationType         | SCC Admin API          | No                        | Yes (via app-only SCC CBA)[web:98][web:64][web:71] | Yes (SCC only)[web:68][web:74] | Inferred yes                        | Yes, with restrictions            | Purely metadata; widely used in automation; good candidate. |
| Get-DlpSensitiveInformationTypeRulePackage | SCC Admin API        | No                        | Yes                | Yes (SCC PowerShell)[web:71][web:74] | Inferred yes                        | Yes, with restrictions            | Returns XML rule packages; responses can be large. |
| Get-DlpSiDetectionsReport               | SCC Admin API          | No                        | Possibly (but cmdlet is being retired)[web:69][web:72] | Partial (marked “will be retired”)[web:69][web:72] | Inferred yes                        | No (reporting surface changing)   | Microsoft recommends Export-ActivityExplorerData instead; building on this is fragile.[web:69][web:78][web:92] |
| Get-MailDetailDlpPolicyReport           | Exchange Admin API (proxy) | No                     | Yes (Exchange.ManageAsApp & RBAC)[web:1][web:7][web:89] | Yes (EXO PowerShell)[web:82][web:89] | Yes (similar mail detail reports shown via Admin API actions)[web:10] | Yes, with restrictions            | Admin API metadata shows `GetMailDetailDlpPolicyReport` action; still undocumented; treat as fragile. |
| Import-DlpComplianceRuleCollection      | SCC Admin API (inferred; legacy) | Unknown          | Unknown           | Unclear; not surfaced in current docs (likely legacy) | Unknown                           | No                               | No clear cloud support; ignore for EXO; redesign using modern Purview export/import. |
| Migrate-DlpFingerprint                  | SCC Admin API / replaced | Unknown                | Unknown           | Not documented as cmdlet; migration now via Set-DlpSensitiveInformationType / portal.[web:120] | Unknown                           | No                               | Treat as superseded by Set-DlpSensitiveInformationType‑based migration; don’t depend on this name. |
| New-CustomDlpEmailTemplate              | SCC Admin API          | No                        | Yes (in principle) | Yes (SCC PowerShell)[web:38]  | Inferred yes                        | Yes, with restrictions            | Write operation; better run via orchestrated PowerShell to stay supported. |
| New-DlpCompliancePolicy                 | SCC Admin API          | No                        | Yes (CBA/app‑only supported generally)[web:64][web:98][web:47][web:55] | Yes (SCC only)[web:47][web:55] | Inferred yes                        | Yes, with restrictions            | Core DLP construct; but write semantics and validation are complex; prefer PS backend. |
| New-DlpComplianceRule                   | SCC Admin API          | No                        | Yes                | Yes (SCC only)[web:52]        | Inferred yes                        | Yes, with restrictions            | As above; strongly prefer structured PS. |
| New-DlpFingerprint                      | SCC Admin API          | No                        | Yes (CBA supported for SCC)[web:98][web:64][web:107][web:110] | Yes (SCC only)[web:107][web:120] | Inferred yes                        | Yes, with restrictions            | Carries file bytes; better to keep inside PS so the module handles encoding/threshold config. |
| New-DlpKeywordDictionary                | SCC Admin API          | No                        | Yes                | Yes (SCC only)[web:105][web:111] | Inferred yes                        | Yes, with restrictions            | Pure metadata + UTF‑16 file data; safe with strong validation. |
| New-DlpSensitiveInformationType         | SCC Admin API          | No                        | Yes                | Yes (SCC only)[web:71][web:120] | Inferred yes                        | Yes, with restrictions            | Central to classification; writes should be guarded with approvals. |
| New-DlpSensitiveInformationTypeRulePackage | SCC Admin API        | No                        | Yes                | Yes (SCC PowerShell; rule‑pack ops)[web:74][web:71] | Inferred yes                        | Yes, with restrictions            | Complex XML; error‑prone; you’ll want pre‑validation and versioning. |
| Remove-CustomDlpEmailTemplate           | SCC Admin API          | No                        | Yes                | Yes (SCC PowerShell)[web:38]  | Inferred yes                        | Yes, with restrictions            | Soft “dangerous” operation; enforce RBAC & approvals. |
| Remove-DlpCompliancePolicy              | SCC Admin API          | No                        | Yes                | Yes (SCC)[web:38]             | Inferred yes                        | Yes, with restrictions            | Same as New‑*: implement 2‑phase delete or approvals. |
| Remove-DlpComplianceRule                | SCC Admin API          | No                        | Yes                | Yes (SCC)[web:38][web:52]     | Inferred yes                        | Yes, with restrictions            | As above. |
| Remove-DlpKeywordDictionary             | SCC Admin API          | No                        | Yes                | Yes (SCC)[web:117]            | Inferred yes                        | Yes, with restrictions            | Moderate risk. |
| Remove-DlpSensitiveInformationType      | SCC Admin API          | No                        | Yes                | Yes (SCC)[web:120]            | Inferred yes                        | Yes, with restrictions            | Could break many policies; require strong governance. |
| Remove-DlpSensitiveInformationTypeRulePackage | SCC Admin API     | No                        | Yes                | Yes (SCC)[web:74]             | Inferred yes                        | Yes, with restrictions            | Same caveats as rule‑pack creation. |
| Set-CustomDlpEmailTemplate              | SCC Admin API          | No                        | Yes                | Yes (SCC)[web:38]             | Inferred yes                        | Yes, with restrictions            | Simple metadata + content update. |
| Set-DlpCompliancePolicy                 | SCC Admin API          | No                        | Yes                | Yes (SCC)[web:57]             | Inferred yes                        | Yes, with restrictions            | Used heavily; treat as high‑risk write. |
| Set-DlpComplianceRule                   | SCC Admin API          | No                        | Yes                | Yes (SCC)[web:52]             | Inferred yes                        | Yes, with restrictions            | As above. |
| Set-DlpKeywordDictionary                | SCC Admin API          | No                        | Yes                | Yes (SCC)[web:108][web:114]   | Inferred yes                        | Yes, with restrictions            | Same as New‑DlpKeywordDictionary. |
| Set-DlpSensitiveInformationType         | SCC Admin API          | No                        | Yes                | Yes (SCC)[web:80][web:120]    | Inferred yes                        | Yes, with restrictions            | Key path for fingerprint migration; must handle `FileData` bytes correctly. |
| Set-DlpSensitiveInformationTypeConfig   | SCC Admin API          | No                        | Yes                | Yes (SCC; global SIT config)  | Inferred yes                        | Yes, with restrictions            | Tenant‑wide behavior; treat as “dangerous op”. |
| Set-DlpSensitiveInformationTypeRulePackage | SCC Admin API       | No                        | Yes                | Yes (SCC)[web:71][web:74]     | Inferred yes                        | Yes, with restrictions            | Rule‑pack manipulation; as above. |
| Test-DlpPolicies                        | SCC Admin API / Purview workload | Likely yes (delegated strongly preferred) | App-only: Unknown / unlikely | Partial (doc covers usage; no HTTP API)[web:35][web:67][web:73] | Unknown                           | Yes, **approximate only**       | Cmdlet tests DLP policies against specific SPO/ODB items; likely proxied to SCC, but no public evidence; functionality may be better approximated via Export-ActivityExplorerData. |
| Test-DataClassification                 | SCC Admin API / EXO    | Likely yes (delegated)    | App-only: not documented    | Yes (Exchange Online + SCC)[web:31][web:102][web:120] | Unknown                           | Yes, with restrictions / approximate | Test cmdlet invokes MS classification engine for supplied text or TestTextExtractionResults. No public HTTP API; must assume proxy under the hood. |
| Test-TextExtraction                     | SCC Admin API / EXO    | Likely yes (delegated)    | App-only: not documented    | Yes (Exchange Online + SCC)[web:18][web:21][web:27] | Unknown                           | Yes, with restrictions / approximate | Takes file bytes and returns extracted text; no known web API; safest to run via PowerShell in backend. |
| Test-Message                            | Exchange Admin API (proxy) | Probably yes (delegated; cmdlet is ExO-only)[web:53][web:46][web:50] | App-only: Unknown             | Yes (EXO only)[web:53][web:46]      | Unknown                           | Yes, with restrictions            | Cmdlet simulates transport rule + DLP evaluation on test messages; high-value but complex; no direct REST surface. |
```

Because of space, this table assumes:

- All **SCC‑only cmdlets** are REST‑proxied via `ps.compliance.protection.outlook.com/adminapi/.../InvokeCommand` when using the modern module, but that proxy remains **undocumented / unsupported** for direct use.
- All **EXO‑only cmdlets** run via `outlook.office365.com/adminapi/.../InvokeCommand` in REST mode, unless retired.

***

## D. Exact auth and token architecture

### Overall model

For a **public multi‑tenant web app** that executes Purview/EXO/SCC operations in the user’s context, the cleanest and most supportable model is:

- **Browser**: SPA (React/Next/etc.) using MSAL.js for Entra ID sign‑in.
- **Backend API**: Confidential client (ASP.NET / Node / Python) running in Azure (Container Apps / App Service) that exposes an HTTPS API consumed by the SPA.
- **PowerShell worker**: Invoked inside the backend (same container or sibling service) that uses **ExchangeOnlineManagement** and **Connect‑ExchangeOnline / Connect‑IPPSSession** to run the actual cmdlets with delegated or app‑only auth.

You *can* also have the backend call the **Admin API directly**, but I’ll keep that as an optional, higher‑risk adapter.

### Entra app registration model

I recommend **two app registrations**:

1. **SPA app (public client)**
    - Type: SPA / Public client (since it runs in browser).
    - Redirect URIs: `https://yourapp.com/auth/callback`, `https://localhost:3000/auth/callback`.
    - Configured to allow account types **“Accounts in any organizational directory”** (multi‑tenant).
    - Permissions:
        - Microsoft Graph delegated scopes: `openid`, `profile`, `email`, `offline_access` and a custom API scope for your backend, e.g. `api://{backend-app-id}/user_impersonation`.
    - No direct permissions to Exchange / SCC; the SPA never calls those resources.
2. **Backend API app (confidential client)**
    - Type: Web / API.
    - Redirect URL: your backend’s base URL for OAuth (used for OBO if needed).
    - Expose API: define a scope like `user_impersonation`. The SPA requests this.
    - API permissions:
        - **Office 365 Exchange Online**:
            - Delegated: `Exchange.ManageV2` (for calling **official v2.0 Admin API** in delegated scenarios).[^1_19][^1_14]
            - App‑only: `Exchange.ManageAsAppV2` if you plan app‑only for limited EXO admin endpoints.[^1_14][^1_19]
        - For SCC / compliance:
            - There is no dedicated “Admin API” entry, but for **Security \& Compliance PowerShell app‑only** you assign **Exchange.ManageAsApp** to the app and then grant an appropriate **Compliance‑related Entra / Exchange RBAC role**.[^1_16][^1_37]
        - Graph: as needed for non‑DLP purposes (user/tenant discovery, etc.).
    - Secrets / credentials:
        - Use **certificate credential** in production (aligned with EXO/SCC CBA guidance).[^1_37][^1_16]

### Browser sign‑in \& tenant selection

1. User navigates to your SPA.
2. SPA uses MSAL.js to sign in via the SPA app registration, requesting:
    - `openid profile email offline_access`
    - `api://{backend-app-id}/user_impersonation`
3. On successful sign‑in, SPA receives:
    - ID token (user identity, tenant id).
    - Access token **for your backend API** (audience = backend app).
4. SPA calls your backend with that access token (Bearer) and any additional context (selected tenant, etc.). You do **not** expose Exchange or SCC tokens to the browser.

**Multi‑tenant nuance**: For guests / multi‑tenant scenarios, you’ll need to honor the `tid` claim and keep per‑tenant configuration (what’s allowed, what policies they enabled). Conditional Access is enforced by Entra before issuing tokens; your app sees the post‑CA result.

### Backend: delegated vs OBO

There are two patterns:

1. **Standard delegated + direct token acquisition in backend (recommended)**
    - Backend receives the SPA token that authorizes calls *to backend only*.
    - Backend uses **MSAL confidential client** with:
        - Client credential (certificate).
        - Delegated “on‑behalf‑of” *or* standard authorization code with `offline_access`.
    - For Exchange Admin API (v2.0) or EXO module:
        - Acquire token for `https://outlook.office365.com/.default` with delegated or app‑only, depending on use.[^1_19][^1_10]
    - For SCC / compliance:
        - Acquire token for `https://ps.compliance.protection.outlook.com/.default` in delegated or app‑only mode, as appropriate.[^1_16][^1_37][^1_3]

**OBO support:** While Microsoft’s Admin API docs do not explicitly call out the on‑behalf‑of grant, they describe using standard OAuth2 delegated flows, and the resources (`outlook.office365.com`, `ps.compliance.protection.outlook.com`) are AAD‑protected like other APIs. In practice, OBO should work as long as the backend app has the right API permissions and the SPA token is issued to that backend. You should *explicitly test this per scenario* and fall back to front‑channel user auth for Exchange if OBO behaves unexpectedly.[^1_14][^1_19]
2. **SPA-acquired resource tokens passed to backend (less secure; avoid if possible)**
    - SPA acquires tokens directly for `https://outlook.office365.com/.default` or `https://ps.compliance.protection.outlook.com/.default` and POSTs them to backend.
    - This couples your frontend strongly to Exchange/SCC and gives the browser tokens powerful enough to manage the tenant; it also complicates CA and auditing.
    - Use this only as a last resort; the **backend‑centric OBO model** is preferable.

### Delegated vs app-only for these cmdlets

- **DLP / SIT management (Get/New/Set/Remove-*)**
    - Supported and widely used via **delegated SCC PowerShell**.[^1_23][^1_24][^1_26][^1_7]
    - **App‑only SCC** is supported for many of these via Connect‑IPPSSession with certificate‑based auth and Exchange.ManageAsApp, plus a Purview/Compliance role like Compliance Administrator assigned to the service principal.[^1_38][^1_37][^1_16]
    - For a user‑driven SaaS, **delegated** is safer: you keep per‑admin RBAC, and actions are auditable as that user.
- **eDiscovery “search-only” cmdlets**
    - Require `‑EnableSearchOnlySession` and the `dataservice.o365filtering.com` audience, **delegated only**; client‑credentials fail due to missing claims.[^1_12][^1_15]
    - So any cmdlets that *depend* on that endpoint must be run with **user‑delegated tokens**.
- **Test cmdlets (Test-TextExtraction, Test-DataClassification, Test-DlpPolicies, Test-Message)**
    - Docs show them as **Exchange Online / SCC cmdlets with user permissions**; no app‑only examples are given.[^1_39][^1_35][^1_36][^1_34][^1_32]
    - Given they run user‑centric evaluation (subject to per‑user access, location scoping, etc.), they should be treated as **delegated‑only** for your web app.


### Conditional Access, MFA, CAE, device claims

Using **delegated flows with OBO** means:

- **MFA / CA** policies apply to the user when authenticating to your SPA; if they pass CA, your backend’s OBO token has the necessary claims.
- **Continuous Access Evaluation (CAE)** for EXO/SCC will invalidate tokens early if risk/user state changes; your backend must handle 401/403 and replay OBO or re‑trigger frontend login.
- Device claims (`xms_mirid`, etc.) will only flow if CA requires them and the SPA uses a compliant client; they do not fundamentally break OBO.


### Token relay \& storage

- Do **not** store Exchange/SCC access tokens in your database. Use:
    - Short‑lived tokens (1h) with automatic renewal via refresh tokens or OBO.
    - Backend caching in memory per user/tenant session if needed.
- Only store **your own session identifiers** and high‑level consent/tenant config.
- All token handling stays in backend; SPA only ever sees tokens scoped to your API.


### Preserving user context \& RBAC

- All delegated tokens must be for the **actual admin user** operating the UI; do not impersonate them via a service principal.
- For app‑only scenarios, clearly bracket which operations are being done as a service principal and ensure:
    - Service principal is granted minimal RBAC roles (e.g., dedicated role group for DLP policy management).[^1_40][^1_18][^1_37]
    - Actions are logged and surfaced in your own audit log with both SP identity and initiator user (if any).

***

## E. Hosting architecture in Azure

### Options

1. **Azure Container Apps (recommended)**
    - Pros:
        - Runs **containerized backend API + PowerShell worker** with auto‑scaling, Dapr sidecars, and simple secret integration via managed identity.
        - Works well with **long‑running, I/O‑bound operations** such as DLP evaluation or classification tests.
        - No need to manage Kubernetes control plane; cheaper and simpler than AKS.
        - Supports **multi‑revision deployments** for safe rollouts and blue‑green.
    - Cons:
        - Slightly more DevOps overhead than pure Functions/App Service, but you’re likely comfortable with containers.
2. **Azure App Service (Web App)**
    - Pros:
        - Straightforward for hosting an ASP.NET / Node backend.
        - Can run PowerShell inside the web app (but mixing interactive PS and HTTP controllers gets messy).
    - Cons:
        - Less natural for running **durable background jobs** and PowerShell runspaces for long operations.
        - Scaling and job isolation are coarser than with Container Apps.
3. **Azure Functions**
    - Pros:
        - Good for **short**, event‑driven operations.
    - Cons:
        - DLP / classification / Test‑Message invocations may exceed normal function execution limits and require careful design with Durable Functions, which complicates things.
        - Loading ExchangeOnlineManagement + Entra MSAL overhead per function cold start is significant.
4. **AKS**
    - Overkill unless you need extensive custom networking / multi‑cluster / sidecars beyond what Container Apps provides.

### Recommended hosting design

- **Frontend**:
    - Azure Static Web Apps or App Service for SPA hosting, integrated with Entra ID.
- **Backend API**:
    - **Azure Container Apps** running:
        - HTTP API service (REST) exposing your command execution endpoints.
        - Internal worker (same container or separate app) responsible for:
            - Managing PowerShell runspaces.
            - Connecting to EXO/SCC via ExchangeOnlineManagement.
            - Queue‑driven execution of commands (for long‑running / async).
- **Secrets \& identity**:
    - Managed identity for Container Apps to:
        - Access Key Vault for certs.
        - Call Entra OAuth endpoints (client credentials) if needed.
- **File upload**:
    - For `Test-TextExtraction` / `Test-Message`:
        - SPA uploads file/message samples via **HTTPS to backend**, which streams them to:
            - Ephemeral storage (Azure Files / local volume) or
            - In‑memory Byte[] passed directly into PowerShell cmdlets (`-FileData`, `-MessageFileData`).[^1_36][^1_34][^1_39]
        - No long‑term retention of file content; log pointers / hashes only.


### Non‑functional aspects

- **Scalability**:
    - Container Apps scale out by HTTP QPS or custom metrics (queue length).
    - For heavy classification tests, dedicate a separate worker app scaling on queue depth.
- **Observability**:
    - Application Insights for:
        - Request traces, dependencies (Admin API / PowerShell), exceptions.
        - Custom telemetry: command name, tenant id (hashed), correlation IDs.
- **Long-running commands**:
    - Use an **internal job queue** (Service Bus / Storage queues) and return an operation id immediately.
    - Client polls a status endpoint or uses WebSockets/SignalR for updates.
- **Rate limits \& throttling**:
    - Admin API and EXO/SCC impose throttling; implement backoff and concurrency caps per tenant.
    - Use `Retry-After` if provided.[^1_19][^1_14]

***

## F. Command execution architecture

### High-level model

- **Command allowlist**: Only expose the exact set of cmdlets (or HTTP adapters) you pre‑approve. No arbitrary PowerShell.
- **Per‑cmdlet adapters**: For each allowed cmdlet:
    - Define a **strongly typed parameter schema** (JSON) that maps to a bounded subset of the real cmdlet parameters.
    - Implement an adapter that:
        - Validates and normalizes input.
        - Constructs either:
            - A **PowerShell invocation** (`Test-TextExtraction -FileData <byte[]>`) inside a runspace; or
            - An **HTTP `CmdletInput` payload** if you opt to call Admin API directly.
        - Calls the backend (PowerShell or HTTP) and normalizes output into your own DTOs.


### Parameter schema \& validation

- Use a schema language (e.g. OpenAPI + JSON schema) to define each command’s parameters:
    - Types (string, enum, arrays, file, datetime).
    - Constraints (patterns for GUIDs, allowed SIT names, etc.).
- Apply:
    - **Server‑side validation** before running any cmdlet.
    - **Tenant‑level policy** (e.g., disallow New/Remove in lower environments, restrict to compliance admins).


### PowerShell vs raw HTTP admin calls

- **Preferred**: **PowerShell orchestration inside backend**:
    - You stay inside the **supported ExchangeOnlineManagement / Connect‑IPPSSession surface**.[^1_17][^1_8][^1_9]
    - Module authors own the mapping from cmdlet to Admin API; if they change it, your code keeps working.
- **Optional**: **HTTP adapters for selected read‑only/report operations**:
    - For high‑volume reporting (e.g., mail detail DLP reports, transport rule reports), calling `/adminapi/beta/...` or `/adminapi/v2.0/...` directly can be more efficient, provided you accept the support risk.[^1_2][^1_14][^1_3]
    - Mark these paths as **“experimental/unsupported internally”** and ensure your contracts can degrade gracefully.


### Dry-run \& idempotency

- **Dry-run** where cmdlets support simulation:
    - DLP policies can be run in **Test modes (TestWithNotifications / TestWithoutNotifications)** before enforcement.[^1_41][^1_42][^1_7]
    - Expose that explicitly in your UI (mode toggles) and API; do not “simulate” by yourself without using the native semantics.
- **Idempotency**:
    - For write operations, design your adapters to be idempotent where possible:
        - Use upsert semantics based on policy/rule IDs.
        - Compare current vs desired state and only apply diffs.


### Serialization \& response normalization

- For PowerShell:
    - Get structured objects back and map them to your DTOs, stripping away PS type metadata and internal properties (`Format*`, etc.).
- For HTTP:
    - Admin API returns JSON with OData wrappers; unwrap `value` arrays and standardize field names.
- Define a **canonical result envelope**:

```json
{
  "operationId": "guid",
  "cmdlet": "Get-DlpCompliancePolicy",
  "tenantId": "hashed",
  "status": "Succeeded|Failed|Running",
  "startedAt": "ISO8601",
  "completedAt": "ISO8601",
  "result": { ... typed payload ... },
  "errors": [ { "code": "...", "message": "...", "category": "Throttling|Auth|Cmdlet|Validation" } ]
}
```


### Error classification \& throttling

- Distinguish:
    - **Auth/consent errors** (401/403, missing RBAC role).
    - **Throttling** (429 with Retry‑After).[^1_14][^1_19]
    - **Cmdlet validation errors** (bad parameters).
    - **Backend transient errors**.
- Implement:
    - Exponential backoff and limited retries per tenant + cmdlet.
    - Hard caps on concurrent executions per tenant for expensive cmdlets (e.g., large classification runs).


### Audit logging \& correlation

- For each command:
    - Log:
        - Tenant, user UPN (hashed but recoverable by support), cmdlet name, high‑level parameters (no secrets/file content), start/end timestamps, outcome.
        - Correlation IDs from EXO/SCC responses when present.
- Correlation:
    - Propagate a `X-Correlation-ID` from the frontend down through backend -> PowerShell -> Admin API, and log it everywhere.


### Tenant/user scoping \& safety

- No command executes outside the calling user’s tenant; the token resource and tenant id ensure that.
- For multi‑tenant SaaS, enforce **per‑tenant isolation in your storage \& queues**, so operations for one tenant cannot see or affect another.


### Where PowerShell remains unavoidable

PowerShell is effectively unavoidable for:

- **Test cmdlets** (Test‑TextExtraction, Test‑DataClassification, Test‑Message, Test‑DlpPolicies):
    - No documented HTTP equivalents; everything we know is via PowerShell docs.[^1_35][^1_43][^1_34][^1_39][^1_32][^1_36]
- Any cmdlets that:
    - Are **not surfaced in official Admin API v2.0**.
    - Have complex parameter binding semantics (hashtables, PSObjects) that are not documented for REST (e.g. `GrantSendOnBehalfTo` hash notation, complex DLP rule conditions).[^1_24][^1_10]

In those cases, the safest approach is to **run PowerShell inside your container** (pwsh 7 + ExchangeOnlineManagement) and not try to reverse‑engineer JSON payloads.

***

## G. Deep dive: Test-TextExtraction / Test-DataClassification / Test-DlpPolicies / Test-Message

### 1. Are these directly proxyable via SCC admin APIs?

- All four are **Exchange / SCC cmdlets** with cloud support documented only via PowerShell:
    - `Test-TextExtraction`: Available in Exchange Online \& Security \& Compliance; `-FileData` takes Byte[] content; examples chain into Test‑DataClassification.[^1_44][^1_34][^1_39]
    - `Test-DataClassification`: Available in Exchange Online \& SCC; tests classification on text or prior `TestTextExtractionResults`.[^1_45][^1_33][^1_35]
    - `Test-DlpPolicies`: Purview DLP test cmdlet for SPO/ODB items, exposed in Purview docs.[^1_43][^1_46][^1_32]
    - `Test-Message`: EXO‑only cmdlet simulating mail flow rules and unified DLP rules; only available in Exchange Online PowerShell.[^1_47][^1_48][^1_36]
- There is **no official documentation** of HTTP equivalents for these; they are not in the v2.0 Admin API endpoints list.[^1_2][^1_14]

Given the general REST proxy behavior for EXO/SCC, it is **very likely** that the module executes them via `/adminapi/beta/.../InvokeCommand` on either EXO (`outlook.office365.com`) or SCC (`ps.compliance.protection.outlook.com`). But since:[^1_8][^1_9][^1_10][^1_3]

- No public metadata examples reference these cmdlets.
- Microsoft has not documented parameter‑to‑JSON mapping for them.

…direct proxying is **possible but fully reverse‑engineered** and can break silently.

### 2. Hidden service calls or non-public behavior?

These cmdlets clearly depend on **internal classification and transport pipelines**:

- `Test-TextExtraction`:
    - “returns the text from unencrypted email message files”; docs note the “Microsoft classification engine uses the results to classify content and determine sensitive information types”.[^1_34][^1_39][^1_44]
- `Test-DataClassification`:
    - “lets you know the classification results… the sensitive type, its count, and confidence.”[^1_33][^1_35][^1_45]
- `Test-DlpPolicies`:
    - Evaluates DLP policies against a specific SharePoint/OneDrive item identified by `-FileUrl` and specified `-Workload`, sending a report to `-SendReportTo`.[^1_46][^1_32][^1_43]
- `Test-Message`:
    - “simulates and reports on the effects of mail flow rules and unified DLP rules on test email messages”; it actually injects a message into the DLP evaluation pipeline and can trigger block/moderate actions and notifications, though reports are directed to `-SendReportTo`.[^1_48][^1_47][^1_36]

That classification \& transport behavior is implemented in **backend services** which are not exposed as direct HTTP APIs. The cmdlets are, therefore, **thin shells** into a complex multi‑step pipeline.

### 3. Can they be reproduced functionally rather than literally?

Approximation ideas:

- **Test-TextExtraction**:
    - You could run your own **text extraction pipeline** (MimeKit + Office format parsers, etc.), but you would not match **exactly** how Microsoft’s classification engine tokenizes and normalizes content for SIT evaluation.[^1_33][^1_34]
- **Test-DataClassification**:
    - You could implement custom regex/keyword‑based classification approximating some SIT logic, but you would lose:
        - Microsoft’s built‑in SIT definitions and thresholds.[^1_27][^1_23][^1_24]
        - ML‑based classifiers, trainable classifiers, partial/exact fingerprint matching semantics.[^1_33]
- **Test-DlpPolicies**:
    - You could:
        - Call Graph/SharePoint APIs to read file contents.
        - Apply your own classification logic.
        - Look up Purview DLP policies \& rules via SCC cmdlets and attempt to “simulate” them.
    - But you’d be replicating a large part of Purview’s rule engine with high risk of divergence.
- **Test-Message**:
    - You could:
        - Parse EML/MIME with a library.
        - Apply a simplified evaluation of mail flow rules obtained from `Get-TransportRule` and DLP rules from Purview.
    - But **transport pipeline behavior** (including ETR precedence, connectors, anti‑spam, etc.) is not trivially reproducible.

Conclusion: **only loose functional approximations are possible**; exact fidelity to Purview’s evaluation is only attainable by **calling the native cmdlets (or their proxies)**.

### 4. Closest cloud‑safe implementation pattern for each

**Recommended pattern** for all four:

- **Backend PowerShell worker** that:
    - Connects via `Connect-ExchangeOnline` (for Test‑Message, and possibly Test‑TextExtraction / Test‑DataClassification) and `Connect-IPPSSession` (for Test‑DlpPolicies) using the signed‑in admin’s delegated context.
    - Runs the cmdlets directly with:
        - `-FileData` or `-MessageFileData` parameters fed from backend‑received bytes.
        - `-TextToClassify` for free‑text tests.
        - `-Workload`, `-FileUrl`, `-SendReportTo` for Test‑DlpPolicies.[^1_39][^1_35][^1_32][^1_36][^1_43][^1_46][^1_34]
    - Parses and normalizes the **output objects** into JSON for your UI.

Only where you have strong appetite for reverse‑engineering and the cmdlet is read‑only would I consider an **HTTP InvokeCommand adapter**; even then, you’d still be hitting the same backend but in a less supported fashion.

### 5. Test-TextExtraction: file flow \& results

**File flow:**

1. User uploads `.eml`, `.msg`, `.docx` etc. via SPA.
2. SPA sends file to backend via HTTPS; backend:
    - Validates max size (e.g., Purview fingerprinting limits 4 MB, etc.).[^1_34][^1_33]
    - Converts to Byte[] and passes into the PS runspace as `-FileData ([System.IO.File]::ReadAllBytes(<path>))` or direct Byte[].[^1_44][^1_39][^1_34]
3. Backend runs:

```powershell
$content = Test-TextExtraction -FileData $bytes
```

4. The cmdlet returns an object whose `.ExtractedResults` property contains the extracted text.[^1_39][^1_44][^1_34]

**Where bytes go:**

- Under the hood, Exchange/SCC module sends those bytes in a serialized form over HTTPS to the EXO or SCC endpoint; you don’t control or see that HTTP call when using PowerShell. This is **exactly what you want** from a supportability standpoint.

**Returned structure \& UI normalization:**

- The documentation and examples show `$content.ExtractedResults` being used as plain text input into Test‑DataClassification.[^1_34][^1_39]
- You can normalize results as:

```json
{
  "rawExtractedText": "string",
  "sizeBytes": 12345,
  "truncated": false
}
```

- Optionally show:
    - First N characters,
    - Basic stats (word count),
    - A download option for the extracted text file.


### 6. Test-DataClassification: output model

Docs and examples show:

```powershell
$r = Test-DataClassification -TextToClassify "Credit card ... SSN ..."
$r.ClassificationResults
```

The `.ClassificationResults` contain, per match:[^1_35][^1_45][^1_33]

- Sensitive information type name / GUID.
- Confidence level (e.g., 65, 85).
- Count of matches.

Combined with `Get-DlpSensitiveInformationType` you can map GUIDs to human‑readable SIT names.[^1_49][^1_23][^1_24]

**UI model:**

- For each result, surface:

```json
{
  "sensitiveInfoTypeId": "GUID",
  "sensitiveInfoTypeName": "Credit Card Number",
  "count": 3,
  "maxConfidence": 85,
  "evidenceSnippets": null
}
```

- Note: The cmdlet does **not** by default return full match values (e.g., full card numbers) for security; you should not attempt to re‑extract them.

Yes, you can power a UI similar to Purview’s “Test” pane with this data, as long as you trust the cmdlet outputs.

### 7. Test-Message: MIME submission \& simulation

Docs \& blog posts clarify:[^1_47][^1_48][^1_36]

- `Test-Message` can be run with:
    - Only `-Sender` / `-Recipients` (body and subject defaulted).
    - Or with a `-MessageFileData` Byte[] representing an `.eml` sample message.

Example:

```powershell
$data = [System.IO.File]::ReadAllBytes('C:\Data\test.eml')
Test-Message -MessageFileData $data `
  -Sender megan@contoso.com -Recipients adele@contoso.com `
  -SendReportTo admin@contoso.com -TransportRules -UnifiedDlpRules
```

- The cmdlet injects the test message into the **actual Exchange transport pipeline**, evaluating:
    - Transport rules (mail flow rules).
    - Unified DLP rules.
    - Purview retention/sensitivity actions.[^1_50][^1_48][^1_47]
- The generated report is **emailed to `-SendReportTo`**; there’s no documented structured object return beyond success/failure.[^1_48][^1_36]

For your app:

- **MIME/EML submission is feasible**: same `-MessageFileData` pattern as Test‑TextExtraction/PS examples; just pass the bytes.
- The test **does not send mail to real recipients**; it simulates rule processing and reports, but because it uses the real pipeline, some actions (e.g., journaling, logging) may still occur.[^1_36][^1_47][^1_48]

This is one of the **most valuable but riskiest** operations to expose; it should only be accessible to high‑privilege admins and perhaps only in test tenants.

***

## H. Security and compliance analysis

Key risks of exposing delegated admin execution via a public web app:

- **Privilege concentration**: A bug or auth misconfiguration could allow a tenant admin to perform operations they normally can’t, or cross‑tenant data confusion.
- **Abuse of powerful cmdlets**: Create/Remove‑DlpCompliancePolicy/Rule, change SITs, etc., can drastically change a tenant’s data protection posture.
- **Token misuse**: If Exchange/SCC tokens are leaked or misused, an attacker gains broad administrative capabilities.

Mitigations \& design choices:

- **Least privilege \& RBAC mapping**:
    - Only let users with **appropriate Purview / Exchange roles** in their own tenant use the web app for that tenant:
        - DLP Compliance Management, Compliance Administrator, etc.[^1_51][^1_7][^1_24][^1_9]
    - Backend never tries to “elevate” beyond what Entra RBAC allows; if a cmdlet fails for lack of role, surface that clearly.
- **Tenant admin onboarding \& consent**:
    - Require global admin / compliance admin to:
        - Approve the Entra app’s permissions.
        - Configure which **cmdlet groups** are enabled in your app (e.g., “read‑only DLP”, “test operations”, “write operations with approvals”).
- **Command-level authorization**:
    - Implement an internal **policy engine** that, per tenant, whitelists:
        - Which cmdlets are enabled.
        - Which locations/workloads they can target.
        - Whether approvals are required.
- **Anti‑CSRF \& anti‑replay**:
    - All browser calls use standard anti‑CSRF tokens + same‑site cookies where appropriate.
    - Backend requires a valid access token for your API on every call and maintains idempotency tokens for write operations.
- **File handling \& retention**:
    - Store uploaded samples only in **ephemeral storage**; delete immediately after the result is produced or after a short TTL.
    - Do not log content; log only metadata (hash, size, type).
    - If tenants require data residency, deploy regionally and route EXO/SCC calls to the correct regional endpoints (e.g., `eur0xb.ps.compliance.protection.outlook.com`).[^1_2][^1_3]
- **Audit trail**:
    - Log who did what, when, and against which objects.
    - Offer export/API of your logs so customers can correlate with Purview audit logs.
- **Microsoft support boundaries**:
    - Using **ExchangeOnlineManagement + Connect‑ExchangeOnline / Connect‑IPPSSession** is supported and recommended for automation.[^1_17][^1_20][^1_9][^1_8]
    - Calling `/adminapi/beta/.../InvokeCommand` directly for arbitrary cmdlets is **explicitly undocumented and unsupported**, even though it is technically identical to what the module does under the hood.[^1_1][^1_2][^1_3]
    - Relying heavily on undocumented endpoints risks:
        - Silent behavioral changes.
        - Broken payload schemas.
        - “No support” answers from Microsoft if tenants open tickets.
- **Operational blast radius**:
    - If Microsoft changes Admin API internals, only the **raw HTTP adapter** layer breaks; your **PowerShell‑based path** should continue to function (module updates hide details).
    - Design your adapters so that unreachable endpoints degrade gracefully and can be toggled off per tenant.

***

## I. Best-practice recommendation

Given the research:

1. **Pure direct admin API proxy app (Option 1)**
    - Technically powerful, but heavily dependent on undocumented `/adminapi/beta/.../InvokeCommand` for SCC/Purview and for most EXO cmdlets.
    - High breakage and support risk; I do **not** recommend this as the primary architecture.
2. **Hybrid app – documented APIs + selected admin proxies (Option 2)**
    - Use:
        - Microsoft Graph, official Exchange Admin API v2.0, Export‑ActivityExplorerData, etc., wherever available \& sufficient.[^1_52][^1_53][^1_54][^1_2][^1_14]
        - Admin API /InvokeCommand *only for specific, read‑oriented/reporting scenarios* where the risk is acceptable and the benefit is large (e.g., mail detail DLP reports, certain transport rule reports).[^1_1][^1_3]
        - **PowerShell sessions in a container** for write operations and for all DLP test cmdlets.
    - This is a reasonable compromise.
3. **Constrained worker around supported PowerShell (Option 3)**
    - Build your web app as a **front‑end to structured Exchange/SCC PowerShell automation**:
        - All cmdlets executed through ExchangeOnlineManagement with REST mode.
        - No direct calls to /adminapi by your code.
    - Strongest supportability; minimal risk from internal API changes.

**Recommendation:**

- **Primary path: Option 3 (PowerShell‑orchestrated worker), with Option 2’s HTTP admin API adapters only for *carefully selected* read‑only scenarios.**
- Explicitly *avoid* using undocumented admin API proxies for write operations or test cmdlets like Test‑TextExtraction, Test‑DataClassification, Test‑DlpPolicies, and Test‑Message, and instead expose them via structured PowerShell runspaces in your backend.

This balances:

- **Supportability**: You stay within the official module’s contract.
- **Breakage risk**: If Microsoft alters REST proxies, the module is updated; your code still works.
- **Time to value**: You can start quickly using existing PS cmdlets and gradually add Admin API shortcuts for high‑volume reads.
- **Security**: You control exactly which cmdlets and parameters can be run, with full auditing and tenant isolation.

**Go / no‑go:**

- **Go** for a **multi‑tenant web UX over a constrained PowerShell + REST backend**, using direct Admin API calls only where they are documented (v2.0 Admin API) or for limited reporting.
- **No‑go** for a pure undocumented admin‑proxy app that treats `/InvokeCommand` as a stable public API.

***

## J. Implementation starter pack

### 1. Reference architecture (text diagram)

- **User browser (SPA)**
    - React/Next app
    - Auth via MSAL.js (SPA app registration)

↓ HTTPS (Bearer access token to backend)
- **API Gateway / Backend service (Container App A)**
    - ASP.NET / Node API
    - Validates tokens
    - Enqueues command requests to per‑tenant queue
    - Exposes:
        - `/api/commands/execute` (async)
        - `/api/commands/{id}` (status/result)

↓ Service Bus / Queue
- **Command worker (Container App B)**
    - Runs pwsh 7 + ExchangeOnlineManagement
    - Maintains pool of PowerShell runspaces per tenant
    - Connects using:
        - `Connect-ExchangeOnline` for EXO cmdlets.
        - `Connect-IPPSSession` for SCC/Purview.
    - Executes allow‑listed cmdlets with validated parameters
    - Optionally calls Admin API v2.0 or `/adminapi/beta` for select read‑only ops
    - Writes normalized results to a result store (Cosmos DB / table / blob)

↓ Result store
- **Storage \& support services**
    - Azure Blob / Files for temporary file uploads
    - Cosmos DB / SQL for:
        - Tenant configs
        - Command logs \& audit records
        - Command results (small payloads)
    - Application Insights for telemetry


### 2. Component list

- SPA: React or Next.js, MSAL.js, TypeScript.
- Backend API: ASP.NET Core or Node.js/Express with OpenAPI.
- Command worker: pwsh 7 + ExchangeOnlineManagement module in a Linux container.
- Azure infrastructure:
    - Container Apps environment.
    - Storage account (blob/file).
    - Service Bus (or Storage queues).
    - Key Vault for certs/secrets.
    - App Insights.


### 3. Backend API surface proposal

Examples:

- `POST /api/commands/execute`

```json
{
  "cmdlet": "Get-DlpCompliancePolicy",
  "tenantId": "GUID-or-hash",
  "parameters": {
    "Identity": "PII Limited",
    "IncludeRulesMetadata": true
  }
}
```

- `GET /api/commands/{operationId}`

```json
{
  "operationId": "guid",
  "status": "Succeeded",
  "cmdlet": "Get-DlpCompliancePolicy",
  "result": [ ... ],
  "errors": [ ... ]
}
```

- Higher‑level endpoints:
    - `POST /api/dlp/policies/test` → orchestrates `Test-DlpPolicies`.
    - `POST /api/classification/test-text` → `Test-DataClassification`.
    - `POST /api/classification/test-file` → `Test-TextExtraction`+`Test-DataClassification`.
    - `POST /api/messaging/test-message` → `Test-Message`.

Each of these maps to one or more internal PowerShell invocations, not arbitrary script text.

### 4. Canonical JSON schema for command execution

```json
{
  "type": "object",
  "properties": {
    "cmdlet": {
      "type": "string",
      "enum": [
        "Get-DlpCompliancePolicy",
        "Get-DlpComplianceRule",
        "Get-DlpSensitiveInformationType",
        "New-DlpCompliancePolicy",
        "Set-DlpCompliancePolicy",
        "Test-TextExtraction",
        "Test-DataClassification",
        "Test-DlpPolicies",
        "Test-Message"
      ]
    },
    "parameters": {
      "type": "object",
      "additionalProperties": false
    }
  },
  "required": ["cmdlet", "parameters"]
}
```

Per‑cmdlet schemas further constrain `parameters` with allowed keys and types.

### 5. Suggested repo structure

```text
/ (root)
  /frontend
    /src
      /components
      /pages
      /auth
      /api-client
    package.json
  /backend
    /src
      /Controllers
      /Models
      /Auth
      /CommandExecution
      /Dlp
      /Classification
    appsettings.json
  /worker
    /src
      /Runspaces
      /CmdletAdapters
      /Scc/Exchange
      /Logging
      /QueueHandlers
    Dockerfile
  /infra
    /bicep-or-terraform
    /pipelines
  /docs
    /architecture.md
    /cmdlet-mapping.md
```


### 6. Phased delivery plan

- **Phase 0: token acquisition + one read‑only cmdlet**
    - Implement SPA sign‑in and backend OBO.
    - Backend worker connects to SCC via Connect‑IPPSSession using delegated auth.
    - Implement `Get-DlpSensitiveInformationType` adapter and simple UI listing SITs.[^1_23][^1_49][^1_9]
- **Phase 1: read‑only DLP/SIT cmdlets**
    - Add adapters for:
        - `Get-DlpCompliancePolicy`, `Get-DlpComplianceRule`, `Get-DlpKeywordDictionary`, `Get-DlpSensitiveInformationTypeRulePackage`, `Get-MailDetailDlpPolicyReport`.[^1_26][^1_55][^1_56][^1_22][^1_52][^1_24]
    - Provide filters, policy/rule drill‑down UI, basic reporting.
- **Phase 2: test operations**
    - Implement file upload endpoints and PS adapters for:
        - `Test-TextExtraction`, `Test-DataClassification`, `Test-DlpPolicies`, `Test-Message` in delegated mode only.[^1_32][^1_43][^1_46][^1_35][^1_47][^1_48][^1_36][^1_39][^1_34]
    - Build UX around:
        - Upload test samples.
        - View extracted text and classification results.
        - View DLP policy match reports and Test‑Message rule evaluation output.
- **Phase 3: write operations with approvals**
    - Add structured adapters for:
        - `New/Set/Remove-DlpCompliancePolicy` \& Rule, SITs, keyword dictionaries, fingerprints.[^1_57][^1_58][^1_28][^1_29][^1_30][^1_59][^1_31][^1_41][^1_24][^1_23]
    - Implement:
        - Per‑tenant change approval workflow.
        - Change plans and diff views.
        - Audit logging and rollback (e.g., export current policy definitions before changes).


### 7. Testing strategy

- **Unit tests**:
    - Adapters: mapping from JSON params to PS cmdlet invocations.
    - Validation logic \& schema enforcement.
- **Integration tests (per tenant / environment)**:
    - Use a dedicated test tenant with synthetic policies and SITs.
    - Validate that:
        - `Get-` cmdlets via your app match direct PowerShell output.
        - Test‑TextExtraction / Test‑DataClassification results match manual PowerShell runs for known samples.
        - Test‑Message outputs expected reports for constructed test rules.
- **Security tests**:
    - Pen‑test the API to ensure:
        - No arbitrary PowerShell execution.
        - No cross‑tenant leakage.
        - Token misuse is prevented.


### 8. Monitoring and rollback

- **Monitoring**:
    - App Insights dashboards per:
        - Cmdlet name.
        - Tenant.
        - Failure class (auth/throttle/cmdlet error).
    - Alerts on:
        - Spike in 4xx/5xx to EXO/SCC.
        - Sustained failures for key cmdlets.
- **Rollback**:
    - For write operations:
        - Before each change, export current policy/rule/SIT definitions (e.g., via Get‑* cmdlets) and store as JSON snapshots.
        - Provide a one‑click “rollback” that re‑applies prior state (again via PS).
    - For code:
        - Use Container Apps revisions with traffic split; can roll back to previous revision instantly if a bug appears.

***

## K. Codex build prompt

Here is a single, consolidated prompt you can paste into ChatGPT/Codex to start building the app:

> You are building a greenfield, production‑ready multi‑tenant web application for Microsoft 365 administrators. The app’s purpose is to provide a browser‑based UI that lets authenticated Microsoft 365 admins run a constrained set of Microsoft Purview / Security \& Compliance / Exchange Online PowerShell cmdlets, specifically around DLP policies, sensitive information types, keyword dictionaries, fingerprints, and test operations (Test‑TextExtraction, Test‑DataClassification, Test‑DlpPolicies, Test‑Message).
>
>
> Architectural constraints and requirements:
>
> 1. **Stack and hosting**
>    - Frontend: React or Next.js SPA in TypeScript.
>    - Backend API: ASP.NET Core Web API (C\#) or Node.js/TypeScript.
>    - Command worker: PowerShell 7 + ExchangeOnlineManagement module running in a Linux container.
>    - Deploy backend API and worker using **Azure Container Apps**; frontend via Azure Static Web Apps or App Service.
>    - Use Azure Service Bus or Azure Storage Queues between API and worker to support async/long‑running commands.
>
> 2. **Authentication and authorization**
>    - Use Microsoft Entra ID with **two app registrations**:
>      - SPA app (public client) for browser sign‑in using MSAL.js.
>      - Backend API app (confidential client) that exposes an `user_impersonation` scope and holds API permissions for:
>        - Office 365 Exchange Online: delegated `Exchange.ManageV2`; app‑only `Exchange.ManageAsAppV2` (for limited documented Admin API v2.0 usage).
>      - For Security \& Compliance / Purview cmdlets, the worker will use:
>        - `Connect-IPPSSession` with delegated tokens and, optionally, certificate‑based app‑only where officially supported.
>    - Implement **on‑behalf‑of (OBO) flow** where possible: SPA acquires tokens only for the backend; backend then requests tokens for Exchange Online (`https://outlook.office365.com/.default`) and the Purview compliance endpoint (`https://ps.compliance.protection.outlook.com/.default`).
>    - Do **not** give the SPA direct tokens for Exchange or SCC; all Exchange/SCC tokens are obtained and used exclusively in the backend.
>
> 3. **Command execution model**
>    - Do **not** allow arbitrary PowerShell scripts from the user.
>    - Implement a **strict cmdlet allowlist** covering at least:
>      - Get‑/New‑/Set‑/Remove‑ DlpCompliancePolicy, DlpComplianceRule, DlpSensitiveInformationType, DlpSensitiveInformationTypeRulePackage, DlpKeywordDictionary, DlpFingerprint.
>      - Get‑DlpSiDetectionsReport (read‑only reporting, understanding it is being retired).
>      - Get‑MailDetailDlpPolicyReport (read‑only report).
>      - Test‑TextExtraction, Test‑DataClassification, Test‑DlpPolicies, Test‑Message (delegated only).
>    - For each cmdlet, define a **typed parameter schema** in code (C\# models or TS interfaces), validating inputs server‑side before execution.
>    - In the worker, map each allowed cmdlet to a **PowerShell adapter** function that:
>      - Accepts a DTO with validated parameters.
>      - Opens or reuses a PowerShell runspace connected via `Connect-ExchangeOnline` or `Connect-IPPSSession` using the current user’s delegated context.
>      - Executes the cmdlet and returns structured objects to the backend.
>    - Prefer this PowerShell‑orchestrated path over direct HTTP calls to `/adminapi/.../InvokeCommand`. Only where Microsoft provides documented Admin API v2.0 endpoints should you use HTTP directly, and then with a small, separate adapter layer.
>
> 4. **API design**
>    - Backend API endpoints:
>      - `POST /api/commands/execute` – accepts a JSON body with `cmdlet` and `parameters`, validates them, enqueues a job, and returns an `operationId`.
>      - `GET /api/commands/{operationId}` – returns an envelope with status and normalized result or errors.
>      - Higher‑level endpoints:
>        - `/api/dlp/policies` – list, detail.
>        - `/api/dlp/policies/test` – orchestrate `Test-DlpPolicies`.
>        - `/api/classification/test-text` – wrap `Test-DataClassification`.
>        - `/api/classification/test-file` – `Test-TextExtraction` + `Test-DataClassification`.
>        - `/api/messaging/test-message` – wrap `Test-Message`.
>    - Define a canonical operation result envelope with fields: `operationId`, `cmdlet`, `tenantId` (hashed), `status`, `startedAt`, `completedAt`, `result`, `errors`.
>
> 5. **UI requirements**
>    - Authenticated admins can:
>      - View DLP policies, rules, SITs, keyword dictionaries, and fingerprints.
>      - Upload sample files/messages for:
>        - Text extraction and classification testing.
>        - DLP policy evaluation on SPO/ODB items.
>        - Mail flow / DLP rule simulation via `Test-Message`.
>    - Render extracted text and classification results in a clear, read‑only way, highlighting:
>      - Sensitive information types.
>      - Counts and confidence levels.
>    - Provide confirmation and, optionally, an approval workflow for write operations (New/Set/Remove‑*).
>
> 6. **Security**
>    - Enforce tenant isolation based on `tid` and user claims from the ID token.
>    - Implement anti‑CSRF protections on all frontend‑to‑backend calls.
>    - Never log raw file contents or full sensitive match values; log only hashes and high‑level metadata.
>    - Implement per‑tenant configuration that limits which cmdlets (and which parameter combinations) are enabled in your UI.
>    - All backend calls to Exchange/SCC must be auditable; log cmdlet name, high‑level parameters, user UPN, and timestamps.
>
> 7. **Admin API usage**
>    - Implement an **internal adapter layer** for Admin API v2.0 calling patterns, but **only for documented endpoints** (Mailbox, MailboxFolderPermission, etc.).
>    - For undocumented `/adminapi/beta/.../InvokeCommand` usage:
>      - Restrict to selected **read‑only/reporting** scenarios.
>      - Isolate these adapters behind a feature flag and clear interfaces so they can be turned off without breaking the rest of the system.
>      - Do not rely on them for DLP write operations or test cmdlets.
>
> 8. **Testing \& quality**
>    - Write unit tests for:
>      - Parameter validation and mapping from JSON → cmdlet models.
>      - PowerShell adapters (mock ExchangeOnlineManagement).
>    - Write integration tests that run against a test tenant, asserting that:
>      - Your API’s outputs match direct PowerShell runs for representative cmdlets and test data.
>    - Include CI/CD definitions to build and deploy:
>      - Frontend.
>      - Backend API container.
>      - Worker container.
>      - Azure Bicep/Terraform for Container Apps, Service Bus, Key Vault, and Storage.
>
> 9. **Documentation**
>    - Generate OpenAPI/Swagger docs for the backend API.
>    - Add markdown documentation:
>      - Architecture overview.
>      - Cmdlet coverage matrix (which are supported and whether they use PowerShell or Admin API adapters).
>      - Security model and required Microsoft 365 roles.
>
>
> Start by scaffolding the repository (frontend, backend, worker, infra) and stubbing the `Get-DlpSensitiveInformationType` command end‑to‑end (UI → API → queue → worker → EXO/SCC → normalized JSON result). Then iterate to add more cmdlets and test operations, following the constraints above and keeping Admin API usage strictly separated behind explicit adapter interfaces.
<span style="display:none">[^1_100][^1_101][^1_102][^1_103][^1_104][^1_105][^1_106][^1_107][^1_108][^1_109][^1_110][^1_111][^1_112][^1_113][^1_114][^1_115][^1_116][^1_117][^1_118][^1_119][^1_120][^1_121][^1_122][^1_123][^1_124][^1_125][^1_60][^1_61][^1_62][^1_63][^1_64][^1_65][^1_66][^1_67][^1_68][^1_69][^1_70][^1_71][^1_72][^1_73][^1_74][^1_75][^1_76][^1_77][^1_78][^1_79][^1_80][^1_81][^1_82][^1_83][^1_84][^1_85][^1_86][^1_87][^1_88][^1_89][^1_90][^1_91][^1_92][^1_93][^1_94][^1_95][^1_96][^1_97][^1_98][^1_99]</span>

<div align="center">⁂</div>

[^1_1]: https://michev.info/blog/post/5822/all-you-need-to-know-about-exchange-online-admin-api-or-how-to-run-cmdlets-without-powershell

[^1_2]: https://learn.microsoft.com/en-us/exchange/reference/admin-api-overview

[^1_3]: https://michev.info/blog/post/7180/all-you-need-to-know-about-exchange-online-admin-api-or-how-to-run-cmdlets-without-powershell-part-2

[^1_4]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Export-DlpPolicyCollection.md

[^1_5]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/import-dlppolicycollection?view=exchange-ps

[^1_6]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Import-DlpPolicyCollection.md

[^1_7]: https://www.jorgebernhardt.com/create-manage-dlp-policies/

[^1_8]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/connect-ippssession?view=exchange-ps

[^1_9]: https://learn.microsoft.com/en-us/powershell/exchange/scc-powershell?view=exchange-ps

[^1_10]: https://michev.info/blog/post/3883/exchange-online-powershell-module-gets-rid-of-the-winrm-dependence

[^1_11]: https://michev.info/blog/post/2869/abusing-the-rest-api-endpoints-behind-the-new-exo-cmdlets

[^1_12]: https://michev.info/blog/post/6742/upcoming-changes-to-the-connect-ippssession-cmdlet-the-enablesearchonlysession-switch

[^1_13]: https://learn.microsoft.com/en-us/powershell/module/exchange/import-dlppolicycollection?view=exchange-ps

[^1_14]: https://learn.microsoft.com/en-us/exchange/reference/admin-api-get-started

[^1_15]: https://app.cloudscout.one/evergreen-item/mc1131771/

[^1_16]: https://learn.microsoft.com/en-us/powershell/exchange/app-only-auth-powershell-v2?view=exchange-ps

[^1_17]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/docs-conceptual/exchange-online-powershell-v2.md

[^1_18]: https://practical365.com/exchange-admin-api/

[^1_19]: https://learn.microsoft.com/en-us/exchange/reference/admin-api-authentication

[^1_20]: https://office365itpros.com/2023/04/17/compliance-endpoint-powershell/

[^1_21]: https://www.linkedin.com/pulse/updated-microsoft-purview-ediscovery-cmdlet-change-van-grondelle-vq1ve

[^1_22]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/get-dlpcompliancepolicy?view=exchange-ps

[^1_23]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/get-dlpsensitiveinformationtype?view=exchange-ps

[^1_24]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/ExchangePowerShell/New-DlpSensitiveInformationType.md

[^1_25]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Get-DlpSiDetectionsReport.md

[^1_26]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Get-DlpCompliancePolicy.md

[^1_27]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/set-dlpsensitiveinformationtype?view=exchange-ps

[^1_28]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/new-dlpkeyworddictionary?view=exchange-ps

[^1_29]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/new-dlpfingerprint?view=exchange-ps

[^1_30]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/set-dlpkeyworddictionary?view=exchange-ps

[^1_31]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/New-DlpKeywordDictionary.md

[^1_32]: https://learn.microsoft.com/en-us/purview/dlp-test-dlp-policies

[^1_33]: https://learn.microsoft.com/en-us/purview/sit-document-fingerprinting

[^1_34]: https://learn.microsoft.com/vi-vn/powershell/module/exchangepowershell/test-textextraction?view=exchange-ps

[^1_35]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/test-dataclassification?view=exchange-ps

[^1_36]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/test-message?view=exchange-ps

[^1_37]: https://michev.info/blog/post/3796/connect-to-the-security-and-compliance-center-powershell-via-certificate-based-authentication

[^1_38]: https://laurakokkarinen.com/how-to-use-security-compliance-powershell-with-application-permissions-on-azure-functions/

[^1_39]: https://learn.microsoft.com/sk-sk/powershell/module/exchangepowershell/test-textextraction?view=exchange-ps

[^1_40]: https://michev.info/blog/post/5599/new-management-roles-exchange-online-rbac

[^1_41]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/new-dlpcompliancepolicy?view=exchange-ps

[^1_42]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/set-dlpcompliancepolicy?view=exchange-ps

[^1_43]: https://learn.microsoft.com/th-th/purview/dlp-test-dlp-policies

[^1_44]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Test-TextExtraction.md

[^1_45]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Test-DataClassification.md

[^1_46]: https://learn.microsoft.com/ja-jp/purview/dlp-test-dlp-policies?view=o365-worldwide

[^1_47]: https://office365itpros.com/2023/02/27/test-message-exchange-cmdlet/

[^1_48]: https://techcommunity.microsoft.com/blog/exchange/how-to-troubleshoot-exchange-online-transport-rules-using-the-test-message-cmdle/4000219

[^1_49]: https://www.agrenpoint.com/microsoft-purview-custom-sit-via-powershell/

[^1_50]: https://borncity.com/win/2023/12/06/roubleshooting-exchange-online-transport-rules-new-cmdlet-test-message/

[^1_51]: https://connectioncloudsupport.zendesk.com/hc/en-us/articles/360062309794-Security-Compliance-Create-test-and-tune-a-DLP-policy

[^1_52]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/get-dlpsidetectionsreport?view=exchange-ps

[^1_53]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Get-DlpDetectionsReport.md

[^1_54]: https://www.manageengine.com/microsoft-365-management-reporting/kb/get-dlp-email-matches-in-microsoft-365.html

[^1_55]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/get-maildetaildlppolicyreport?view=exchange-ps

[^1_56]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Get-MailDetailDlpPolicyReport.md

[^1_57]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/New-DlpComplianceRule.md

[^1_58]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/New-DlpCompliancePolicy.md

[^1_59]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/ExchangePowerShell/New-DlpFingerprint.md

[^1_60]: https://michev.info/blog/post/7291/psa-more-cmdlets-require-connecting-to-the-scc-endpoint-via-the-enablesearchonlysession-switch

[^1_61]: https://learn.microsoft.com/en-us/powershell/exchange/invoke-command-workarounds-rest-api?view=exchange-ps

[^1_62]: https://michev.info/page/7?p_publisheddate=20140705+07%3A37%3A00\&p_id=32\&pagefirstrow=391\&view=%7B20d638e7-fdd9-4e25-8752-0d81cee20ff1%7D

[^1_63]: https://www.m365.fm/blog/how-to-manage-user-accounts-with-the-microsoft-exchange-online-admin-api/

[^1_64]: https://windowsforum.com/threads/exchange-online-admin-api-public-preview-a-guided-ews-migration-path.389707/

[^1_65]: https://office365itpros.com/2025/08/27/automating-microsoft-365-with-powershell15/

[^1_66]: https://stackoverflow.com/questions/79128548/using-azure-entra-id-i-cant-see-office-365-exchange-online-under-apis-my-orga

[^1_67]: https://learn.microsoft.com/en-nz/answers/questions/5595288/new-compliancesearchaction-fails-after-update-(non

[^1_68]: https://stackoverflow.com/questions/46572112/office-365-outlook-api-exchange-admin-center

[^1_69]: https://techcommunity.microsoft.com/discussions/exchange_general/support-for-unattended-scripting-in-delegation-scenarios-for-exchange-online-pow/3623657

[^1_70]: https://forums.ironmansoftware.com/t/exchangeonline-module-could-not-load-file-or-assembly/8679

[^1_71]: https://michev.info/blog/post/3870/teams-remote-powershell-updates-and-new-api-endpoints

[^1_72]: https://community.powerplatform.com/forums/thread/details/?threadid=1c9ffb17-adb6-431c-a014-0630aa216165

[^1_73]: https://www.reddit.com/r/PowerShell/comments/1cigecr/microsoft_testtextextraction_issues/

[^1_74]: https://www.msxfaq.de/cloud/exchangeonline/betrieb/exchange_online_powershell_v2.htm

[^1_75]: https://learn.microsoft.com/en-za/answers/questions/5602990/some-api-for-litigation-hold-switching-and-status

[^1_76]: https://www.reddit.com/r/golang/comments/1ajytvw/exchange_online_powershell_with_go/

[^1_77]: https://techcommunity.microsoft.com/t5/exchange/support-for-unattended-scripting-in-delegation-scenarios-for/m-p/3625178

[^1_78]: https://techcommunity.microsoft.com/blog/microsoft-security-blog/export-dlp-policies-rules-and-settings-using-powershell/4133230

[^1_79]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/get-dataclassification?view=exchange-ps

[^1_80]: https://www.powershellgallery.com/packages/ZeroTrustAssessment/2.1.51-preview/Content/tests\Test-Assessment.35033.ps1

[^1_81]: https://www.linkedin.com/posts/poiriersimon_export-dlp-policies-rules-and-settings-using-activity-7196225046258876417-O0dF

[^1_82]: https://www.powershellgallery.com/packages/ZeroTrustAssessment/2.1.132-preview/Content/tests\Test-Assessment.35033.ps1

[^1_83]: https://github.com/microsoft/Microsoft365DSC/issues/1767

[^1_84]: https://o365reports.com/connect-to-security-and-compliance-powershell-using-connect-ippssession/

[^1_85]: https://o365reports.com/audit-dlp-policy-matches-in-microsoft-365-using-powershell/

[^1_86]: https://learn.microsoft.com/en-us/answers/questions/5177544/export-and-import-dlp-rules

[^1_87]: https://www.agrenpoint.com/creating-basic-dlp-policies-with-powershell/

[^1_88]: https://www.sebae.net/blog/how-to-troubleshoot-exchange-online-transport-rules-using-the-test-message-cmdlet/

[^1_89]: https://www.pdq.com/blog/what-are-rest-apis/

[^1_90]: https://o365info.com/connect-ippssession/

[^1_91]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/docs-conceptual/invoke-command-workarounds-rest-api.md

[^1_92]: https://www.techtarget.com/searchwindowsserver/tutorial/Learn-to-use-PowerShell-Invoke-RestMethod-for-API-calls

[^1_93]: https://www.agrenpoint.com/creating-advanced-dlp-policies-with-powershell/

[^1_94]: https://learn.microsoft.com/es-es/powershell/module/exchange/get-dlpsidetectionsreport?view=exchange-ps

[^1_95]: https://www.powershellgallery.com/packages/BitTitan.Runbooks.Office365SecurityAndCompliance.Beta/0.2.7/Content/Functions\Get-Office365SecurityAndComplianceSettingObjects.ps1

[^1_96]: https://www.reco.ai/hub/configure-microsoft-365-dlp-policies

[^1_97]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Get-DlpDetailReport.md

[^1_98]: https://stackoverflow.com/questions/35056914/office365-compliance-is-there-a-rest-api-to-fetch-all-the-dlp-policy-and-rules

[^1_99]: https://learn.microsoft.com/en-us/powershell/module/exchangepowershell/get-dlpdetailreport?view=exchange-ps

[^1_100]: https://learn.cloudpartner.fi/posts/section-7-implement-dlp-monitor-and-manage-dlp-activities

[^1_101]: https://admindroid.com/how-to-get-dlp-incident-detail-report-in-exchange-online

[^1_102]: https://community.spiceworks.com/t/powershell-report-for-approved-emails-dlp-rules/933798

[^1_103]: https://office365itpros.com/2025/12/29/dlp-alerts-report/

[^1_104]: https://masterandcmdr.com/2018/11/22/powershell-connect-to-the-security-compliance-center/

[^1_105]: https://learn.microsoft.com/en-us/answers/questions/311679/data-loss-prevention-powershell

[^1_106]: https://learn.microsoft.com/ko-kr/powershell/module/exchange/test-textextraction?view=exchange-ps

[^1_107]: https://www.powershellgallery.com/packages/ComplianceDiagnostics/1.4.4/Content/ComplianceDiagnosticDataClassification.psm1

[^1_108]: https://www.youtube.com/watch?v=Jq4EyASVu08

[^1_109]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Get-DataClassification.md

[^1_110]: https://stackoverflow.com/questions/78592554/get-dlp-policies-from-compliance-microsoft-com

[^1_111]: https://www.powershellgallery.com/packages/ExoHelper/2.0.1-beta3/Content/ExoHelper.psm1

[^1_112]: https://pdhewaju.azurewebsites.net/2018/08/27/dlp-policy-using-exchange-online-powershell/

[^1_113]: https://m365admin.handsontek.net/retiring-import-transportrulecollection-in-exchange-online-powershell/

[^1_114]: https://app.cloudscout.one/evergreen-item/mc672157/

[^1_115]: https://learn.microsoft.com/en-us/exchange/import-a-custom-dlp-policy-template-from-a-file-exchange-2013-help

[^1_116]: https://learn.microsoft.com/en-us/answers/questions/796753/migrate-dlp-from-exchange-online-admin-center-to-m

[^1_117]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Set-DlpKeywordDictionary.md

[^1_118]: https://github.com/microsoft/Microsoft365DSC/issues/2892

[^1_119]: https://techdocs.broadcom.com/us/en/symantec-security-software/identity-security/identity-management-and-governance-connectors/1-0/connectors/microsoft-connectors/microsoft-office-365/connect-to-office-365.html

[^1_120]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Remove-DlpKeywordDictionary.md

[^1_121]: http://unifiedpeople.ru/exch2013onpremhelp.en/html/f6a0aedc-8aac-4c0a-9a4d-09a4823604b6.htm

[^1_122]: https://www.linkedin.com/posts/kotireddyeda_export-dlp-policies-rules-and-settings-using-activity-7194275008204992512-r1En

[^1_123]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/exchange/Import-DlpPolicyTemplate.md

[^1_124]: https://www.markwilson.co.uk/blog/2015/08/export-transport-rules-from-exchange-or-exchange-online.htm

[^1_125]: https://github.com/MicrosoftDocs/office-docs-powershell/blob/main/exchange/exchange-ps/ExchangePowerShell/New-Fingerprint.md

