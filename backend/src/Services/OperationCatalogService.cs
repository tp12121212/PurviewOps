using PurviewOps.Api.Models;

namespace PurviewOps.Api.Services;

public sealed class OperationCatalogService : IOperationCatalogService
{
    private static readonly IReadOnlyList<OperationDefinition> Operations =
        new List<OperationDefinition>
        {
            new("op-get-sit", "Get-DlpSensitiveInformationType", "0", true, false, AuthMode.Delegated, ExecutionTarget.SecurityCompliance, [], "Foundation read-only operation."),
            new("op-get-policy", "Get-DlpCompliancePolicy", "1", true, false, AuthMode.Delegated, ExecutionTarget.SecurityCompliance, [], "Read-only."),
            new("op-get-rule", "Get-DlpComplianceRule", "1", true, false, AuthMode.Delegated, ExecutionTarget.SecurityCompliance, [], "Read-only."),
            new("op-get-dictionary", "Get-DlpKeywordDictionary", "1", true, false, AuthMode.Delegated, ExecutionTarget.SecurityCompliance, [], "Read-only."),
            new("op-get-rulepackage", "Get-DlpSensitiveInformationTypeRulePackage", "1", true, false, AuthMode.Delegated, ExecutionTarget.SecurityCompliance, [], "Read-only."),
            new("op-get-report", "Get-MailDetailDlpPolicyReport", "1", true, false, AuthMode.Delegated, ExecutionTarget.ExchangeOnline, [new("StartDate","date",true,"UTC start date"), new("EndDate","date",true,"UTC end date")], "Read-only reporting."),
            new("op-test-text-extraction", "Test-TextExtraction", "2", true, false, AuthMode.Delegated, ExecutionTarget.ExchangeOnline, [new("FileName","string",true,"Uploaded file name")], "Delegated-only test operation."),
            new("op-test-data-classification", "Test-DataClassification", "2", true, false, AuthMode.Delegated, ExecutionTarget.SecurityCompliance, [new("Text","string",false,"Inline test text")], "Delegated-only test operation."),
            new("op-test-dlp-policies", "Test-DlpPolicies", "2", true, false, AuthMode.Delegated, ExecutionTarget.SecurityCompliance, [], "Delegated-only test operation."),
            new("op-test-message", "Test-Message", "2", true, false, AuthMode.Delegated, ExecutionTarget.ExchangeOnline, [new("Subject","string",true,"Subject"), new("Body","string",true,"Body")], "Delegated-only test operation."),
            new("op-new-policy", "New-DlpCompliancePolicy", "3", true, true, AuthMode.Delegated, ExecutionTarget.SecurityCompliance, [new("Name","string",true,"Policy name")], "Feature-flagged write op with approval/snapshot requirements."),
            new("op-set-policy", "Set-DlpCompliancePolicy", "3", true, true, AuthMode.Delegated, ExecutionTarget.SecurityCompliance, [new("Identity","string",true,"Policy identity")], "Feature-flagged write op with approval/snapshot requirements."),
            new("op-export-collection", "Export-DlpPolicyCollection", "n/a", false, false, AuthMode.Delegated, ExecutionTarget.ExchangeOnline, [], "Unsupported/retired in EXO."),
            new("op-import-collection", "Import-DlpPolicyCollection", "n/a", false, false, AuthMode.Delegated, ExecutionTarget.ExchangeOnline, [], "Unsupported/retired in EXO.")
        }.OrderBy(o => o.OperationName, StringComparer.Ordinal).ToList();

    public IReadOnlyList<OperationDefinition> GetOperations() => Operations;

    public OperationDefinition? FindByName(string operationName) =>
        Operations.FirstOrDefault(o => string.Equals(o.OperationName, operationName, StringComparison.Ordinal));
}
