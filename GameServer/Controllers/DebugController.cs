using System.Data;
using ClosedXML.Excel;
using GameServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Secure this controller
    public class DebugController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly ILogger<DebugController> _logger;

        public DebugController(GameDbContext context, ILogger<DebugController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("tables")]
        public async Task<ActionResult<IEnumerable<string>>> GetTableNames()
        {
            try
            {
                var tableNames = await Task.Run(() =>
                {
                    var names = _context
                        .Model.GetEntityTypes()
                        .Select(t =>
                            t.GetSchema() != null
                                ? $"{t.GetSchema()}.{t.GetTableName()}"
                                : t.GetTableName()
                        )
                        .Where(name => name != null)
                        .Distinct()
                        .ToList();

                    // Filter out EF Core migration history table if present
                    names = names.Where(name => !name.Contains("__EFMigrationsHistory")).ToList();

                    return names;
                });

                return Ok(tableNames);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting table names.");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpGet("table-data/{tableName}")] // New endpoint
        public async Task<IActionResult> GetTableData(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return BadRequest("Table name cannot be empty.");
            }

            try
            {
                var entityType = _context
                    .Model.GetEntityTypes()
                    .FirstOrDefault(e =>
                        (
                            e.GetSchema() != null
                                ? $"{e.GetSchema()}.{e.GetTableName()}"
                                : e.GetTableName()
                        ) == tableName
                    );

                if (entityType == null)
                {
                    return NotFound($"Table '{tableName}' not found or not mapped in DbContext.");
                }

                string schema = entityType.GetSchema() ?? "dbo";
                string actualTableName = entityType.GetTableName();

                string sqlQuery = $"SELECT * FROM [{schema}].[{actualTableName}]";

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = sqlQuery;
                    _context.Database.OpenConnection();

                    var data = new List<Dictionary<string, object>>();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var columnNames = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columnNames.Add(reader.GetName(i));
                        }

                        while (reader.Read())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[columnNames[i]] = reader.GetValue(i);
                            }
                            data.Add(row);
                        }
                    }
                    _context.Database.CloseConnection();
                    return Ok(data); // Return data as JSON
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting data for table '{tableName}'.");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("export-excel")]
        public async Task<IActionResult> ExportToExcel([FromBody] string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return BadRequest("Table name cannot be empty.");
            }

            try
            {
                // Validate table name against known entity types to prevent SQL injection
                // This part needs to be updated to handle schema-qualified names
                var entityType = _context
                    .Model.GetEntityTypes()
                    .FirstOrDefault(e =>
                        (
                            e.GetSchema() != null
                                ? $"{e.GetSchema()}.{e.GetTableName()}"
                                : e.GetTableName()
                        ) == tableName
                    );

                if (entityType == null)
                {
                    return NotFound($"Table '{tableName}' not found or not mapped in DbContext.");
                }

                // Extract schema and table name for the SQL query
                string schema = entityType.GetSchema() ?? "dbo"; // Default to dbo if schema is null
                string actualTableName = entityType.GetTableName();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add(tableName);

                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = $"SELECT * FROM [{schema}].[{actualTableName}]"; // Updated SQL query
                        _context.Database.OpenConnection();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // Add headers
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                worksheet.Cell(1, i + 1).Value = reader.GetName(i);
                            }

                            // Add data
                            int row = 2;
                            while (reader.Read())
                            {
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    worksheet.Cell(row, i + 1).Value = reader
                                        .GetValue(i)
                                        .ToString();
                                }
                                row++;
                            }
                        }
                        _context.Database.CloseConnection();
                    }

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(
                            content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"{tableName}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error exporting table '{tableName}' to Excel.");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpDelete("delete-row/{tableName}/{id}")]
        public async Task<IActionResult> DeleteRow(string tableName, int id)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return BadRequest("Table name cannot be empty.");
            }

            if (tableName != "gameplay.PlayerSessionLog")
            {
                return BadRequest("Deletion is only supported for PlayerSessionLog table.");
            }

            try
            {
                var logEntry = await _context.PlayerSessionLogs.FindAsync(id);
                if (logEntry == null)
                {
                    return NotFound($"Row with ID {id} not found in {tableName}.");
                }

                _context.PlayerSessionLogs.Remove(logEntry);
                await _context.SaveChangesAsync();

                return Ok(
                    new { message = $"Row with ID {id} deleted successfully from {tableName}." }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting row with ID {id} from table {tableName}.");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }
    }
}
