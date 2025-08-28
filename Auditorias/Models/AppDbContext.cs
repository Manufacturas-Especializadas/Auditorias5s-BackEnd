using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Auditorias.Models;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Answers> Answers { get; set; }

    public virtual DbSet<Audits> Audits { get; set; }

    public virtual DbSet<Forms> Forms { get; set; }

    public virtual DbSet<Offices> Offices { get; set; }

    public virtual DbSet<PeripheralArea> PeripheralArea { get; set; }

    public virtual DbSet<ProductionLines> ProductionLines { get; set; }

    public virtual DbSet<Questions> Questions { get; set; }

    public virtual DbSet<Sections> Sections { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Answers>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Answers__3214EC07A2C92086");

            entity.Property(e => e.Score).HasColumnName("score");

            entity.HasOne(d => d.IdAuditNavigation).WithMany(p => p.Answers)
                .HasForeignKey(d => d.IdAudit)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Answers__IdAudit__07C12930");

            entity.HasOne(d => d.IdQuestionNavigation).WithMany(p => p.Answers)
                .HasForeignKey(d => d.IdQuestion)
                .HasConstraintName("FK__Answers__IdQuest__08B54D69");
        });

        modelBuilder.Entity<Audits>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Audits__3214EC07F5CBD3F5");

            entity.Property(e => e.Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("date");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("description");
            entity.Property(e => e.PhotoUrl)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("photoUrl");
            entity.Property(e => e.Responsible)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("responsible");

            entity.HasOne(d => d.IdFormNavigation).WithMany(p => p.Audits)
                .HasForeignKey(d => d.IdForm)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Audits__IdForm__7B5B524B");

            entity.HasOne(d => d.IdOfficesNavigation).WithMany(p => p.Audits)
                .HasForeignKey(d => d.IdOffices)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Audits__IdOffice__09A971A2");

            entity.HasOne(d => d.IdPeripheralAreaNavigation).WithMany(p => p.Audits)
                .HasForeignKey(d => d.IdPeripheralArea)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Audits__IdPeriph__7C4F7684");

            entity.HasOne(d => d.IdProductionLinesNavigation).WithMany(p => p.Audits)
                .HasForeignKey(d => d.IdProductionLines)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Audits__IdProduc__7D439ABD");
        });

        modelBuilder.Entity<Forms>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Forms__3214EC07EF43A403");

            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Offices>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Offices__3214EC07C673717B");

            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("name");
        });

        modelBuilder.Entity<PeripheralArea>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Peripher__3214EC0760A45202");

            entity.Property(e => e.Name)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("name");
        });

        modelBuilder.Entity<ProductionLines>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Producti__3214EC07EB2DC978");

            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Questions>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Question__3214EC075A0EB267");

            entity.Property(e => e.Text)
                .HasColumnType("text")
                .HasColumnName("text");

            entity.HasOne(d => d.IdSectionNavigation).WithMany(p => p.Questions)
                .HasForeignKey(d => d.IdSection)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Questions__IdSec__75A278F5");
        });

        modelBuilder.Entity<Sections>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Sections__3214EC2780D77CE3");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Name)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("name");

            entity.HasOne(d => d.IdFormNavigation).WithMany(p => p.Sections)
                .HasForeignKey(d => d.IdForm)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Sections__IdForm__72C60C4A");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}