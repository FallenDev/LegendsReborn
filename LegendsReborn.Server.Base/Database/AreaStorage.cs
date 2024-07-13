using Darkages.Enums;
using Darkages.ScriptingBase;
using Darkages.Types;
using Microsoft.Data.SqlClient;

namespace Darkages.Database;

public class AreaStorage
{
    public static void CacheFromDatabase()
    {
        try
        {
            const string conn = "Data Source=.;Initial Catalog=Legends;Integrated Security=True;TrustServerCertificate=True";
            var sConn = new SqlConnection(conn);
            const string sql = "SELECT * FROM Legends.dbo.Maps";

            sConn.Open();

            var cmd = new SqlCommand(sql, sConn);
            var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var temp = new Area();
                temp.ID = (int)reader["MapId"];
                var flags = ServiceStack.AutoMappingUtils.ConvertTo<MapFlags>(reader["Flags"]);
                temp.Flags = flags;
                temp.Music = (int)reader["Music"];
                var rows = (int)reader["mRows"];
                temp.Height = (byte)rows;
                var cols = (int)reader["mCols"];
                temp.Width = (byte)cols;
                temp.ScriptKey = reader["ScriptKey"].ToString();
                temp.Name = reader["Name"].ToString();

                var mapFile = Directory.GetFiles($@"{ServerSetup.Instance.StoragePath}\maps", $"lod{temp.ID}.map",
                    SearchOption.TopDirectoryOnly).FirstOrDefault();

                if (!((mapFile != null) && File.Exists(mapFile))) 
                    continue;

                if (!LoadMap(temp, mapFile, true))
                {
                    Console.Write($"Map Load Unsuccessful: {temp.ID}_{temp.Name}");
                    continue;
                }

                if (!string.IsNullOrEmpty(temp.ScriptKey))
                    temp.Scripts = ScriptManager.Load<AreaScript>(temp.ScriptKey, temp);

                ServerSetup.Instance.GlobalMapCache[temp.ID] = temp;
            }

            reader.Close();
            sConn.Close();
        }
        catch (SqlException e)
        {
            Console.WriteLine(e.ToString());
        }

        GameLog.Info($"Maps Loaded: {ServerSetup.Instance.GlobalMapCache.Count.ToString()}");
    }

    private static bool LoadMap(Area mapObj, string mapFile, bool save = false)
    {
        mapObj.FilePath = mapFile;
        mapObj.Data = File.ReadAllBytes(mapFile);
        mapObj.Hash = Crc16Provider.ComputeChecksum(mapObj.Data);

        return mapObj.OnLoaded();
    }

        
}