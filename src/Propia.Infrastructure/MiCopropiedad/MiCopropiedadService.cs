using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

public partial class MiCopropiedadService : IMiCopropiedadService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly Storage.IBlobStorage _blob;
    private readonly Application.UsuariosAccesos.ISeedUsuarioRolService _seed;
    private readonly Application.Directorio.IDirectorioService _dir;
    public MiCopropiedadService(PropiaDbContext db, ITenantContext tenant, Storage.IBlobStorage blob,
        Application.UsuariosAccesos.ISeedUsuarioRolService seed,
        Application.Directorio.IDirectorioService dir)
    {
        _db = db;
        _tenant = tenant;
        _blob = blob;
        _seed = seed;
        _dir = dir;
    }

}
