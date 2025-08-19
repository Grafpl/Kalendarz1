using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Kalendarz1
{
    internal class DaneKogos
    {
        static string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public void UzupełnienieDanychHodowcydoTextBoxow(string idDostawcy, Dictionary<string, TextBox> pola)
        {
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand("SELECT * FROM Hodowcy WHERE ID = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", idDostawcy ?? (object)DBNull.Value);
                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return;

                    // mapa nazw kolumn -> indeks, bez wrażliwości na wielkość liter
                    var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; i++)
                        ordinals[reader.GetName(i)] = i;

                    foreach (var kv in pola)
                    {
                        var key = kv.Key;           // nazwa kolumny z bazy
                        var tb = kv.Value;         // TextBox do wypełnienia

                        if (tb == null) continue;
                        if (!ordinals.TryGetValue(key, out int ord)) continue;

                        string text = reader.IsDBNull(ord) ? "" : Convert.ToString(reader.GetValue(ord));
                        tb.Text = text ?? "";
                    }
                }
            }
        }
    }
}
