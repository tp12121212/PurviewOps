# Operation catalog

Phase 0:
- Get-DlpSensitiveInformationType

Phase 1 read-only:
- Get-DlpCompliancePolicy
- Get-DlpComplianceRule
- Get-DlpKeywordDictionary
- Get-DlpSensitiveInformationTypeRulePackage
- Get-MailDetailDlpPolicyReport

Phase 2 tests:
- Test-TextExtraction
- Test-DataClassification
- Test-DlpPolicies
- Test-Message

Phase 3 controlled writes (feature flagged, approval + snapshot + rollback metadata):
- New/Set-DlpCompliancePolicy
- New/Set-DlpComplianceRule
- New/Set-DlpKeywordDictionary
- New/Set-DlpSensitiveInformationType

Unsupported (retired in EXO):
- Export-DlpPolicyCollection
- Import-DlpPolicyCollection
