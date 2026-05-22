using FlexoCableSV.PuntoVenta.Data;
using FlexoCableSV.PuntoVenta.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexoCableSV.PuntoVenta.Services
{
    class DTEService
    {
        private readonly FlexoDbContext _db;

        public DTEService(FlexoDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Procesa entradas pendientes de la cola de contingencia.
        /// dte_type no está en dte_contingency; se obtiene vía navegación/JOIN con dte_issued.
        /// </summary>
        public async Task ProcessContingencyQueueAsync(CancellationToken cancellationToken = default)
        {
            var pending = await _db.DteContingencies
                .Include(c => c.DteIssued)
                .Where(c => !c.IsResolved && c.NextAttemptAt <= DateTime.UtcNow)
                .OrderBy(c => c.NextAttemptAt)
                .ToListAsync(cancellationToken);

            foreach (var entry in pending)
            {
                var dteType = entry.DteIssued.DteType;
                await ResendContingencyDteAsync(entry, dteType, cancellationToken);
            }
        }

        private static Task ResendContingencyDteAsync(
            DteContingency entry,
            string dteType,
            CancellationToken cancellationToken)
        {
            // TODO: generar payload según dteType ('01','03','05','06'), firmar y enviar a MH
            _ = entry;
            _ = dteType;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
