#region

using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using Chaos.Extensions.Common;
using Dapper;
using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.Sprites;
using Microsoft.Data.SqlClient;
using BindingFlags = System.Reflection.BindingFlags;
#endregion

namespace Darkages.Types.Debuffs;

public abstract class DebuffBase
{
    private static readonly ConcurrentDictionary<string, Func<DebuffBase>> CachedDebuffFactory = new(StringComparer.OrdinalIgnoreCase);
    
    public ushort Animation { get; set; }
    public virtual bool Cancelled { get; set; }
    public virtual byte Icon { get; set; }
    public virtual int Length { get; set; }
    public virtual string Name { get; set; }
    public int TimeLeft { get; set; }
    public GameServerTimer Timer { get; set; }
    public StatusBarColor CurrentColor { get; set; }
    public virtual ICollection<string> Aliases { get; } = new List<string>();
    /// <summary>
    /// Be careful using this, if someone relogs it will be null
    /// </summary>
    public Sprite Source { get; private set; }
    protected DebuffBase() => Timer = new GameServerTimer(TimeSpan.FromSeconds(1.0));

    private bool CheckOnDebuff(IGameClient client, string name)
    {
        try
        {
            const string PROCEDURE = "[SelectDeBuffsCheck]";
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();

            var cmd = new SqlCommand(PROCEDURE, sConn)
            {
                CommandType = CommandType.StoredProcedure
            };

            //cmd.CommandTimeout = 5;
            cmd.Parameters.Add("@Serial", SqlDbType.Int).Value = client.Aisling.Serial;
            cmd.Parameters.Add("@Name", SqlDbType.VarChar).Value = name;

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var debuffName = reader["Name"].ToString();

                if (!string.Equals(debuffName, name, StringComparison.CurrentCultureIgnoreCase))
                    continue;

                return string.Equals(name, debuffName, StringComparison.CurrentCultureIgnoreCase);
            }
        } catch (SqlException e)
        {
            ServerContext.Logger(e.ToString());
            
        } catch (Exception e)
        {
            ServerContext.Logger(e.ToString());
            
        }

        return false;
    }

    protected void DeleteDebuff(Aisling aisling)
    {
        try
        {
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            const string PLAYER_DEBUFFS = "DELETE FROM LegendsPlayers.dbo.PlayersDebuffs WHERE Serial = @Serial AND Name = @Name";

            sConn.Execute(PLAYER_DEBUFFS, new
            {
                aisling.Serial,
                Name
            });

        } catch (SqlException e)
        {
            ServerContext.Logger(e.ToString());
            
        } catch (Exception e)
        {
            ServerContext.Logger(e.ToString());
            
        }
    }

    protected void InsertDebuff(Aisling aisling)
    {
        var continueInsert = CheckOnDebuff(aisling.Client, Name);

        if (continueInsert)
            return;

        // Timer needed to be re-initiated here (do not refactor out)
        Timer = new GameServerTimer(TimeSpan.FromSeconds(1));

        try
        {
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            var adapter = new SqlDataAdapter();
            sConn.Open();
            var deBuffId = Generator.GenerateNumber();
            var debuffNameReplaced = Name.Replace("'", "''");

            var playerDeBuffs = "INSERT INTO LegendsPlayers.dbo.PlayersDebuffs (DebuffId, Serial, Name, TimeLeft) "
                                + $"VALUES ('{deBuffId}','{aisling.Serial}','{debuffNameReplaced}','{TimeLeft}')";

            var cmd = new SqlCommand(playerDeBuffs, sConn);
            adapter.InsertCommand = cmd;
            adapter.InsertCommand.ExecuteNonQuery();
        } catch (SqlException e)
        {
            ServerContext.Logger(e.ToString());
            
        } catch (Exception e)
        {
            ServerContext.Logger(e.ToString());
            
        }
    }

    private static DataTable MappedDataTablePlayersDebuffs(DataTable dataTable, Aisling obj)
    {
        dataTable.Clear();

        dataTable.Columns.Add("Serial");
        dataTable.Columns.Add("Name");
        dataTable.Columns.Add("TimeLeft");

        foreach (var debuff in obj.Debuffs.Values.Where(i => i is { Name: { } }))
        {
            var row = dataTable.NewRow();
            row["Serial"] = obj.Serial;
            row["Name"] = debuff.Name;
            row["TimeLeft"] = debuff.TimeLeft;
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }
    
    internal static DebuffBase CreateInstance(string debuffName)
    {
        if (!ServerContext.GlobalDeBuffCache.ContainsKey(debuffName))
            return null;
        
        var debuffFactory = CachedDebuffFactory.GetOrAdd(debuffName, key =>
        {
            var instance = ServerContext.GlobalDeBuffCache[key];
            var ctor = instance.GetType().GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes);
            var call = Expression.New(ctor!);
            var lambda = Expression.Lambda<Func<DebuffBase>>(call);

            return lambda.Compile();
        });

        return debuffFactory();
    }
    
    //but when you log back in, no way to re-fetch source
    public virtual bool TryApply(Sprite source, Sprite affected)
    {
        if (affected.HasBuff("siolaidh"))
        {
            source.Client?.SendMessage($"A holy aura surrounds this target. [siolaidh]");
            return false;
        }

        foreach (var alias in Aliases.Prepend(Name))
        {
            var existingAlias = TryFindSimilarBuff(alias, affected);

            if (!string.IsNullOrEmpty(existingAlias))
            {
                source.Client?.SendMessage(0x03, $"A similar spell is already in effect. [{existingAlias}]");
                return false;
            }
        }
        Source = source;
        OnApplied(affected);

        return true;
    }
    
    protected string TryFindSimilarBuff(string buffName, Sprite affected)
    {
        buffName = buffName
            .ReplaceI("danaan ")
            .ReplaceI("io ")
            .ReplaceI(" ionad")
            .ReplaceI("dia ")
            .ReplaceI("ard ")
            .ReplaceI("mor ")
            .ReplaceI("beag ")
            .Trim();

        return affected.Debuffs.Keys.FirstOrDefault(buffKey => buffKey.ContainsI(buffName));
    }
    
    public virtual void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (timeLeft.HasValue)
        {
            TimeLeft = timeLeft.Value;
            Timer.Tick = Length - TimeLeft;
        } else
            TimeLeft = Length;

        if (affected is Aisling aisling)
        {
            InsertDebuff(aisling);
            aisling.Client.SendStats(StatusFlags.All);
        }
    }

    public virtual void OnDurationUpdate(Sprite affected)
    {
        if (affected is not Aisling aisling)
            return;

        UpdateDebuff(aisling);
    }

    public virtual void OnEnded(Sprite affected)
    {
        if (affected is not Aisling aisling)
            return;

        aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));

        DeleteDebuff(aisling);
        aisling.Client.SendStats(StatusFlags.All);
    }

    internal void Update(Sprite affected, TimeSpan elapsedTime)
    {
        if (Timer.Disabled)
            return;

        if (!Timer.Update(elapsedTime))
            return;

        if (Length - Timer.Tick > 0)
            OnDurationUpdate(affected);
        else
        {
            OnEnded(affected);
            Timer.Tick = 0;

            return;
        }

        Timer.Tick++;
    }

    protected static void UpdateDebuff(Aisling aisling)
    {
        var dataTable = new DataTable();
        dataTable = MappedDataTablePlayersDebuffs(dataTable, aisling);
        var tableNumber = Generator.GenerateNumber();
        var table = $"TmpTable{tableNumber.ToString()}";

        try
        {
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            using var cmd = new SqlCommand("", sConn);
            sConn.Open();
            //cmd.CommandTimeout = 5;

            cmd.CommandText = $"CREATE TABLE {table}([Serial] INT,[Name] VARCHAR(30),[TimeLeft] INT)";
            cmd.ExecuteNonQuery();

            using var bulkCopy = new SqlBulkCopy(sConn);
            //bulkCopy.BulkCopyTimeout = 5;
            bulkCopy.DestinationTableName = table;
            bulkCopy.WriteToServer(dataTable);

            cmd.CommandText = "BEGIN TRAN; "
                              + "UPDATE P SET P.[Name] = T.[Name], P.[TimeLeft] = T.[TimeLeft] "
                              + $"FROM LegendsPlayers.dbo.PlayersDebuffs AS P INNER JOIN {table} AS T ON P.[Serial] = T.[Serial] AND P.[Name] = T.[Name]; DROP TABLE {table}; "
                              + "COMMIT;";

            cmd.ExecuteNonQuery();
        } catch (SqlException e)
        {
            ServerContext.Logger(e.ToString());
            
        } catch (Exception e)
        {
            ServerContext.Logger(e.ToString());
            
        }
    }
}