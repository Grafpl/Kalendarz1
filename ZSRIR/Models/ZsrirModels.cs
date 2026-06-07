using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kalendarz1.ZSRIR.Models
{
    // ============ AUTH ============
    public class TokenRequest
    {
        [JsonPropertyName("username")] public string Username { get; set; } = "";
        [JsonPropertyName("password")] public string Password { get; set; } = "";
    }

    public class TokenResponse
    {
        // Dokumentacja ZSRIR §2.1: {"token": "...", "expires": "18.12.2024 19:34:42", "type": "Bearer"}
        [JsonPropertyName("token")]   public string? Token { get; set; }
        [JsonPropertyName("expires")] public string? Expires { get; set; }     // string "dd.MM.yyyy HH:mm:ss"
        [JsonPropertyName("type")]    public string? Type { get; set; }        // "Bearer"

        // Wstecznie kompatybilne aliasy (na wypadek innych formatów)
        [JsonPropertyName("accessToken")] public string? AccessToken { get; set; }
        public string ResolvedToken => Token ?? AccessToken ?? "";
    }

    // ============ DataSuppliers (§4.1) ============
    public class DataSupplier
    {
        [JsonPropertyName("id")]         public int Id { get; set; }
        [JsonPropertyName("fullName")]   public string FullName { get; set; } = "";
        [JsonPropertyName("department")] public string? Department { get; set; }
        // Wstecz-kompatybilny alias dla starego UI (DisplayMemberPath="Name")
        [JsonIgnore]
        public string Name => string.IsNullOrEmpty(Department)
            ? FullName
            : $"{FullName} ({Department})";
    }

    // ============ Forms (§4.2) ============
    public class FormInfo
    {
        [JsonPropertyName("id")]   public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";   // np. "Drób rzeźny"
    }

    // ============ ReportingPeriods (§4.3) ============
    public class ReportingPeriod
    {
        [JsonPropertyName("id")]          public int Id { get; set; }
        [JsonPropertyName("dateFrom")]    public DateTime DateFrom { get; set; }      // pn (data początkowa okresu)
        [JsonPropertyName("dateTo")]      public DateTime DateTo { get; set; }        // niedz (data końcowa okresu)
        [JsonPropertyName("dateTimeEnd")] public DateTime DateTimeEnd { get; set; }   // wt 12:00 (deadline raportowania)

        // Pomocnicza — czy okres wciąż otwarty (deadline w przyszłości)
        [JsonIgnore]
        public bool IsOpen => DateTime.Now < DateTimeEnd;
    }

    // ============ FormConfiguration (§4.4) ============
    public class FormConfiguration
    {
        [JsonPropertyName("commodityGroup")] public CommodityGroup? CommodityGroup { get; set; }
        [JsonPropertyName("formFields")]     public List<FormField> FormFields { get; set; } = new();
    }

    public class CommodityGroup
    {
        [JsonPropertyName("id")]              public int Id { get; set; }
        [JsonPropertyName("name")]            public string Name { get; set; } = "";   // np. "Drób", "Kurczęta brojler"
        [JsonPropertyName("commodityGroups")] public List<CommodityGroup> CommodityGroups { get; set; } = new();   // podgrupy (rekurencyjnie)
        [JsonPropertyName("formFields")]      public List<FormField> FormFields { get; set; } = new();
    }

    public class FormField
    {
        [JsonPropertyName("id")]         public int Id { get; set; }                  // KLUCZ używany w AddForm.formFieldsValues
        [JsonPropertyName("name")]       public string Name { get; set; } = "";
        [JsonPropertyName("type")]       public string Type { get; set; } = "";       // Price/Amount/Count/Percent/Description/Option
        [JsonPropertyName("unit")]       public string? Unit { get; set; }            // "PLN/t", "t", "%"
        [JsonPropertyName("isRequired")] public bool IsRequired { get; set; }
        [JsonPropertyName("options")]    public List<FormFieldOption> Options { get; set; } = new();
        [JsonPropertyName("minValue")]   public decimal? MinValue { get; set; }
        [JsonPropertyName("maxValue")]   public decimal? MaxValue { get; set; }
        [JsonPropertyName("remarks")]    public string? Remarks { get; set; }
    }

    public class FormFieldOption
    {
        [JsonPropertyName("id")]          public int Id { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; } = "";
    }

    // ============ AddForm — body wysyłki ============
    public class AddFormRequest
    {
        [JsonPropertyName("formReportingPeriodId")] public int FormReportingPeriodId { get; set; }
        [JsonPropertyName("dataSupplierId")]        public int DataSupplierId { get; set; }
        [JsonPropertyName("forms")]                 public List<FormPayload> Forms { get; set; } = new();
    }

    public class FormPayload
    {
        [JsonPropertyName("commodityGroupId")] public int CommodityGroupId { get; set; }
        // API ZSRIR wymaga wartości jako STRING (nie number) — inaczej 400 "could not be converted to System.String".
        // Decimal serializuj z InvariantCulture (kropka dziesiętna).
        [JsonPropertyName("formFieldsValues")] public Dictionary<string, string> FormFieldsValues { get; set; } = new();
    }

    // ============ AddFormZero — formularz zerowy ============
    public class AddFormZeroRequest
    {
        [JsonPropertyName("formReportingPeriodId")] public int FormReportingPeriodId { get; set; }
        [JsonPropertyName("dataSupplierId")]        public int DataSupplierId { get; set; }
    }

    // ============ Generic API response ============
    public class ApiError
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("errors")]  public Dictionary<string, List<string>>? Errors { get; set; }
    }

    // ============ Historia wysyłek (z ZsrirSubmissions w SQL) ============
    public class SubmissionRow
    {
        public int Id { get; set; }
        public DateTime OkresOd { get; set; }
        public DateTime OkresDo { get; set; }
        public string KategoriaTowaru { get; set; } = "";
        public int? CommodityGroupId { get; set; }
        public decimal KgRazem { get; set; }
        public decimal TonyRazem { get; set; }
        public decimal WartoscNetto { get; set; }
        public decimal CenaZlTona { get; set; }
        public int? FormReportingPeriodId { get; set; }
        public int? DataSupplierId { get; set; }
        public string Status { get; set; } = "Pending";
        public string? ApiResponse { get; set; }
        public string? ErrorMessage { get; set; }
        public int? WyslanyPrzez { get; set; }
        public string? WyslanyPrzezImie { get; set; }   // JOIN z operators
        public DateTime? WyslanyDataCzas { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
