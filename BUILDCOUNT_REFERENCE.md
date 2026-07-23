# BUFFERCOUNT and MAXTRANSFERSIZE Tuning Reference

These settings control SQL Server's memory allocation during `RESTORE DATABASE` operations. Tuning them can significantly reduce restore times for large databases.

## Configuration

Set in `appsettings.json` under `MsSqlConnection`:

```json
"MsSqlConnection": {
    "BufferCount": 1024,
    "MaxTransferSize": 4194304
}
```

---

## BUFFERCOUNT

Controls the number of 8KB memory buffers SQL Server allocates for reading/writing backup data.

**Formula:** `Memory used = BufferCount x 8 KB`

### Recommended Values

| BUFFERCOUNT | Memory Used | Best For |
|-------------|------------|----------|
| 64 | ~512 KB | Small databases (< 1 GB), low-RAM machines |
| 256 | ~2 MB | Medium databases (1 - 10 GB) |
| **1024** | **~8 MB** | **Large databases (10 - 50 GB) - recommended default** |
| 4096 | ~32 MB | Very large databases (50 GB+), dedicated restore server |
| 16384 | ~128 GB | Massive databases (100 GB+), ensure sufficient free RAM |

### Guidelines

- Keep `BufferCount x 8 KB` under **50% of available free RAM** on the SQL Server
- Higher values reduce disk I/O round-trips, speeding up restore
- Too high can cause memory pressure on the SQL Server instance, affecting other workloads
- For shared servers, start with 1024 and increase gradually while monitoring memory

---

## MAXTRANSFERSIZE

Controls the size (in bytes) of each read/write transfer from the backup device. Larger values mean fewer I/O operations.

### Recommended Values

| MAXTRANSFERSIZE | Size | Best For |
|----------------|------|----------|
| 1048576 | 1 MB | Default SQL Server value, low-RAM machines |
| **4194304** | **4 MB** | **Most environments - recommended default** |
| 8388608 | 8 MB | Fast storage (NVMe, high-end SAN) |
| 16777216 | 16 MB | High-throughput networks, dedicated restore infrastructure |

### Guidelines

- Larger transfers reduce the number of I/O calls, improving throughput
- The optimal value depends on storage speed - faster storage benefits more from larger transfers
- Must be a multiple of 65536 (64 KB)
- Combined with BUFFERCOUNT, total memory = `BUFFERCOUNT x MAXTRANSFERSIZE` per outstanding I/O batch

---

## Quick Tuning Examples

### Conservative (shared server, limited RAM)

```json
"BufferCount": 256,
"MaxTransferSize": 1048576
```

Memory: ~2 MB. Safe for shared environments.

### Balanced (default, most use cases)

```json
"BufferCount": 1024,
"MaxTransferSize": 4194304
```

Memory: ~4 MB. Good for databases up to 50 GB.

### Aggressive (dedicated server, fast storage)

```json
"BufferCount": 4096,
"MaxTransferSize": 8388608
```

Memory: ~32 MB. For large databases on dedicated hardware with NVMe/SSD storage.

---

## Monitoring

After changing these values, monitor:

- **Restore duration** - should decrease with higher buffer counts
- **SQL Server memory usage** - ensure `Total Server Memory` does not exceed available RAM
- **Disk I/O** - use Resource Monitor or `sys.dm_io_pending_io_requests` to verify I/O throughput improved
- **Page life expectancy** - if this drops significantly, reduce BUFFERCOUNT
