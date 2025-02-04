﻿#region

using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using Chaos.Extensions.Common;
using Dapper;
using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.Sprites;
using Microsoft.Data.SqlClient;

#endregion

namespace Darkages.Types.Buffs;

public abstract class BuffBase
{
    private static readonly ConcurrentDictionary<string, Func<BuffBase>> CachedBuffFactory = new(StringComparer.OrdinalIgnoreCase);
    public ushort Animation { get; set; }
    public bool Cancelled { get; set; }
    public virtual byte Icon { get; set; }
    public virtual int Length { get; set; }
    public virtual string Name { get; set; }
    public int TimeLeft { get; set; }
    public GameServerTimer Timer { get; set; }
    public StatusBarColor CurrentColor { get; set; }
    public virtual ICollection<string> Aliases { get; set; } = new List<string>();
    /// <summary>
    /// Be careful using this, if someone relogs it will be null
    /// </summary>
    public Sprite Source { get; private set; }
    protected BuffBase() => Timer = new GameServerTimer(TimeSpan.FromSeconds(1));
    
    protected bool CheckOnBuff(IGameClient client)
    {
        try
        {
            const string PROCEDURE = "[SelectBuffsCheck]";
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();

            var cmd = new SqlCommand(PROCEDURE, sConn)
            {
                CommandType = CommandType.StoredProcedure
            };

            //cmd.CommandTimeout = 5;
            cmd.Parameters.Add("@Serial", SqlDbType.Int).Value = client.Aisling.Serial;
            cmd.Parameters.Add("@Name", SqlDbType.VarChar).Value = Name;

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var buffName = reader["Name"].ToString();

                if (string.Equals(buffName, Name, StringComparison.CurrentCultureIgnoreCase))
                    return true;
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

    protected void DeleteBuff(Aisling aisling)
    {
        try
        {
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            sConn.Open();
            const string PLAYER_BUFFS = "DELETE FROM LegendsPlayers.dbo.PlayersBuffs WHERE Serial = @Serial AND Name = @Name";

            sConn.Execute(PLAYER_BUFFS, new
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

    internal static BuffBase CreateInstance(string buffName)
    {
        if (!ServerContext.GlobalBuffCache.ContainsKey(buffName))
            return null;
        
        var debuffFactory = CachedBuffFactory.GetOrAdd(buffName, key =>
        {
            var instance = ServerContext.GlobalBuffCache[key];
            var ctor = instance.GetType().GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes);
            var call = Expression.New(ctor!);
            var lambda = Expression.Lambda<Func<BuffBase>>(call);

            return lambda.Compile();
        });

        return debuffFactory();
    }

    protected void InsertBuff(Aisling aisling)
    {
        var continueInsert = CheckOnBuff(aisling.Client);

        if (continueInsert)
            return;

        // Timer needed to be re-initiated here (do not refactor out)
        Timer = new GameServerTimer(TimeSpan.FromSeconds(1));

        try
        {
            using var sConn = new SqlConnection(AislingStorage.ConnectionString);
            var adapter = new SqlDataAdapter();
            sConn.Open();
            var buffId = Generator.GenerateNumber();
            var buffNameReplaced = Name.Replace("'", "''");

            var playerDeBuffs = "INSERT INTO LegendsPlayers.dbo.PlayersBuffs (BuffId, Serial, Name, TimeLeft) "
                                + $"VALUES ('{buffId}','{aisling.Serial}','{buffNameReplaced}','{TimeLeft}')";

            var cmd = new SqlCommand(playerDeBuffs, sConn);
            //cmd.CommandTimeout = 5;
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

    private static DataTable MappedDataTablePlayersBuffs(DataTable dataTable, Aisling obj)
    {
        dataTable.Clear();

        dataTable.Columns.Add("Serial");
        dataTable.Columns.Add("Name");
        dataTable.Columns.Add("TimeLeft");

        foreach (var buff in obj.Buffs.Values.Where(i => i is { Name: { } }))
        {
            var row = dataTable.NewRow();
            row["Serial"] = obj.Serial;
            row["Name"] = buff.Name;
            row["TimeLeft"] = buff.TimeLeft;
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }

    //but when you log back in, no way to re-fetch source
    public virtual bool TryApply(Sprite source, Sprite affected)
    {
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
            .ReplaceI("io ")
            .ReplaceI(" ionad")
            .ReplaceI("dia ")
            .ReplaceI("ard ")
            .ReplaceI("mor ")
            .ReplaceI("beag ")
            .Trim();

        return affected.Buffs.Keys.FirstOrDefault(buffKey => buffKey.ContainsI(buffName));
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
            InsertBuff(aisling);
            aisling.Client.SendStats(StatusFlags.All);
        }
    }

    public virtual void OnDurationUpdate(Sprite affected)
    {
        if (affected is not Aisling aisling)
            return;

        UpdateBuff(aisling);
        //aisling.Client.SendStats(StatusFlags.All);
        //send stat updates once per update loop of buffs instead
        //see UpdateBuffs
    }

    public virtual void OnEnded(Sprite affected)
    {
        if (affected is not Aisling aisling)
            return;

        aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));

        DeleteBuff(aisling);
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

    protected static void UpdateBuff(Aisling aisling)
    {
        var dataTable = new DataTable();
        dataTable = MappedDataTablePlayersBuffs(dataTable, aisling);
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
                              + $"FROM LegendsPlayers.dbo.PlayersBuffs AS P INNER JOIN {table} AS T ON P.[Serial] = T.[Serial] AND P.[Name] = T.[Name]; DROP TABLE {table}; "
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