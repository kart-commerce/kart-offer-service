namespace KartOfferService.Application.Common.Interfaces;

/// <summary>Resolves the acting principal for `created_by`/`updated_by` audit stamping - either the checkout caller's subject or Admin Service's client-credentials service principal (ADR-0010/ADR-0019).</summary>
public interface ICurrentPrincipal
{
    string ActingPrincipal { get; }
}
