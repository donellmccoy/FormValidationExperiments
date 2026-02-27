using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

class Program {
    static async Task Main() {
        var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true };
        var client = new HttpClient(handler);
        var json = "{\"MemberId\": 1, \"ProcessType\": \"Informal\", \"WorkflowState\": \"MemberInformationEntry\", \"Component\": \"RegularAirForce\", \"IncidentType\": \"Injury\", \"IncidentDate\": \"2023-01-01T00:00:00Z\", \"InitiationDate\": \"2023-01-01T00:00:00Z\", \"FinalFinding\": \"InLineOfDuty\"}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://localhost:7173/odata/Cases", content);
        var responseString = await response.Content.ReadAsStringAsync();
        Console.WriteLine(response.StatusCode);
        Console.WriteLine(responseString);
    }
}
