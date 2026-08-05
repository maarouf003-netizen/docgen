namespace DocGenerator.Domain.Enums;

public enum UserRole
{
    Lawyer = 1,
    Head = 2,
    Manager = 3,
    Admin = 4
}

public enum ContractTypeSelector
{
    Bank = 1,
    Regular = 2
}

public enum ExecutionStatus
{
    None = 0,
    ExecutedForcibly = 1,
    ExecutedBySettlement = 2,
    Deferred = 3
}
