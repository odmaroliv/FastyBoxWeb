using FastyBoxWeb.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Security.Claims;

namespace FastyBoxWeb.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IHttpContextAccessor httpContextAccessor) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Address> Addresses => Set<Address>();
        public DbSet<ForwardRequest> ForwardRequests => Set<ForwardRequest>();
        public DbSet<ForwardItem> ForwardItems => Set<ForwardItem>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<ShippingRate> ShippingRates => Set<ShippingRate>();
        public DbSet<CustomsRate> CustomsRates => Set<CustomsRate>();
        public DbSet<RequestStatusHistory> RequestStatusHistories => Set<RequestStatusHistory>();
        public DbSet<Attachment> Attachments => Set<Attachment>();
        public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
        public DbSet<RequiredDocument> RequiredDocuments => Set<RequiredDocument>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configurar filtros globales
            ConfigureSoftDeleteFilter(builder);
            ConfigureUserScopeFilter(builder);

            // Configuraciones de entidades
            ConfigureAddressEntity(builder);
            ConfigureForwardRequestEntity(builder);
            ConfigurePaymentEntity(builder);
        }

        public override int SaveChanges()
        {
            ApplyAuditInfo();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAuditInfo();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyAuditInfo()
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            var entries = ChangeTracker.Entries()
                .Where(e => (e.Entity is IAuditableEntity || e.Entity is ISoftDelete) &&
                            (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted));

            foreach (var entry in entries)
            {
                if (entry.Entity is IAuditableEntity auditableEntity)
                {
                    if (entry.State == EntityState.Added)
                    {
                        auditableEntity.CreatedAt = DateTime.UtcNow;
                        auditableEntity.CreatedBy = userId;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        auditableEntity.ModifiedAt = DateTime.UtcNow;
                        auditableEntity.ModifiedBy = userId;
                    }
                }

                // Soft delete
                if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete softDeleteEntity)
                {
                    entry.State = EntityState.Modified;
                    softDeleteEntity.IsDeleted = true;
                    softDeleteEntity.DeletedAt = DateTime.UtcNow;
                    softDeleteEntity.DeletedBy = userId;
                }
            }
        }

        private void ConfigureSoftDeleteFilter(ModelBuilder builder)
        {
            // Aplicar filtro de soft delete a todas las entidades que implementan ISoftDelete
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var property = Expression.PropertyOrField(parameter, "IsDeleted");
                    var condition = Expression.Equal(property, Expression.Constant(false));
                    var lambda = Expression.Lambda(condition, parameter);

                    builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
                }
            }
        }

        private void ConfigureUserScopeFilter(ModelBuilder builder)
        {
            // Aplicar filtro de scope de usuario a todas las entidades que extienden UserOwnedEntity
            // Nota: Este filtro se aplicará solo para usuarios normales, no para administradores
            // La lógica para bypassear el filtro para administradores está en los servicios
        }

        private void ConfigureAddressEntity(ModelBuilder builder)
        {
            builder.Entity<Address>()
                .HasOne(a => a.User)
                .WithMany(u => u.Addresses)
            .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        private void ConfigureForwardRequestEntity(ModelBuilder builder)
        {
            builder.Entity<ForwardRequest>()
                .HasOne(fr => fr.User)
                .WithMany(u => u.ForwardRequests)
                .HasForeignKey(fr => fr.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ForwardRequest>()
                .HasOne(fr => fr.ShippingAddress)
                .WithMany()
                .HasForeignKey(fr => fr.ShippingAddressId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ForwardItem>()
                .HasOne(fi => fi.ForwardRequest)
                .WithMany(fr => fr.Items)
                .HasForeignKey(fi => fi.ForwardRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RequestStatusHistory>()
                .HasOne(rsh => rsh.ForwardRequest)
                .WithMany(fr => fr.StatusHistory)
                .HasForeignKey(rsh => rsh.ForwardRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Attachment>()
                .HasOne(a => a.ForwardItem)
                .WithMany(fi => fi.Attachments)
            .HasForeignKey(a => a.ForwardItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RequiredDocument>()
            .HasOne(rd => rd.ForwardRequest)
            .WithMany(fr => fr.RequiredDocuments)
            .HasForeignKey(rd => rd.ForwardRequestId)
            .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RequiredDocument>()
                .HasOne(rd => rd.Attachment)
                .WithOne()
                .HasForeignKey<RequiredDocument>(rd => rd.AttachmentId)
                .OnDelete(DeleteBehavior.SetNull);
        }

        private void ConfigurePaymentEntity(ModelBuilder builder)
        {
            builder.Entity<Payment>()
                .HasOne(p => p.ForwardRequest)
                .WithMany(fr => fr.Payments)
                .HasForeignKey(p => p.ForwardRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Payment>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

