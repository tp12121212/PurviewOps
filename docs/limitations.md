# Limitations

- Worker queue execution is scaffolded; production deployments must wire Azure Queue/Service Bus bindings.
- Initial worker implementation includes one concrete adapter (`Get-DlpSensitiveInformationType`) plus catalog scaffolding.
- Retired EXO cmdlets are intentionally marked unsupported.
