namespace Autonocraft.Village
{
    public static class JobAssignmentReasonCodes
    {
        public const string WrongVillage = "WrongVillage";
        public const string NoQuarry = "NoQuarry";
        public const string NoFarmPlot = "NoFarmPlot";
        public const string NoLumberTarget = "NoLumberTarget";
        public const string NoMineTarget = "NoMineTarget";
        public const string NoPendingSite = "NoPendingSite";
        public const string NoWorkshop = "NoWorkshop";
        public const string NoSmithWork = "NoSmithWork";
        public const string NoKitchen = "NoKitchen";
        public const string NoTarget = "NoTarget";
    }

    public readonly struct JobAssignmentResult
    {
        public bool Success { get; }
        public string ReasonCode { get; }
        public string PlayerMessage { get; }
        public string Remediation { get; }

        private JobAssignmentResult(bool success, string reasonCode, string playerMessage, string remediation)
        {
            Success = success;
            ReasonCode = reasonCode;
            PlayerMessage = playerMessage;
            Remediation = remediation;
        }

        public static JobAssignmentResult Succeeded() =>
            new(true, string.Empty, string.Empty, string.Empty);

        public static JobAssignmentResult Failed(string reasonCode, string playerMessage, string remediation) =>
            new(false, reasonCode, playerMessage, remediation);
    }

    public static class RecruitReasonCodes
    {
        public const string NoCitizens = "NoCitizens";
        public const string NoSettlement = "NoSettlement";
        public const string HousingCap = "HousingCap";
        public const string MissingMaterials = "MissingMaterials";
        public const string CostFailed = "CostFailed";
    }

    public readonly struct RecruitResult
    {
        public bool Success { get; }
        public string ReasonCode { get; }
        public string PlayerMessage { get; }
        public string Remediation { get; }

        private RecruitResult(bool success, string reasonCode, string playerMessage, string remediation)
        {
            Success = success;
            ReasonCode = reasonCode;
            PlayerMessage = playerMessage;
            Remediation = remediation;
        }

        public static RecruitResult Succeeded(string villagerName) =>
            new(true, string.Empty, $"{villagerName} joined! Assign a job on the People tab.", string.Empty);

        public static RecruitResult Failed(string reasonCode, string playerMessage, string remediation) =>
            new(false, reasonCode, playerMessage, remediation);
    }
}
