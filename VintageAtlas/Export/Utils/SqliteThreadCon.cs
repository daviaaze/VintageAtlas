using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace VintageAtlas.Export;

public class SqliteThreadCon
{
    public bool InUse;
    public SqliteConnection Con;
    public readonly DbCommand GetMapChunk;
    public readonly DbCommand GetChunk;
    public readonly DbCommand SaveChunksCmd;

    public SqliteThreadCon(SqliteConnection con)
    {
        Con = con;

        GetMapChunk = con.CreateCommand();
        GetMapChunk.CommandText = "SELECT data FROM mapchunk WHERE position=@position";
        GetMapChunk.Parameters.Add(SavegameDataLoader.CreateParameter("position", DbType.UInt64, 0, GetMapChunk));
        GetMapChunk.Prepare();

        GetChunk = con.CreateCommand();
        GetChunk.CommandText = "SELECT data FROM chunk WHERE position=@position";
        GetChunk.Parameters.Add(SavegameDataLoader.CreateParameter("position", DbType.UInt64, 0, GetChunk));
        GetChunk.Prepare();

        SaveChunksCmd = con.CreateCommand();
        SaveChunksCmd.CommandText = "INSERT OR REPLACE INTO mapchunk (position, data) VALUES (@position,@data)";
        SaveChunksCmd.Parameters.Add(SavegameDataLoader.CreateParameter("position", DbType.UInt64, 0, SaveChunksCmd));
        SaveChunksCmd.Parameters.Add(SavegameDataLoader.CreateParameter("data", DbType.Object, null, SaveChunksCmd));
        SaveChunksCmd.Prepare();

        InUse = false;
    }

    public void Free()
    {
        InUse = false;
    }
}