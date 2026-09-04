namespace Farol.Web.Configuration;

/// <summary>
/// Tetos do sistema. O custo do Farol é função destes números, não da
/// quantidade de sites que alguém consiga cadastrar.
/// </summary>
public class FarolOptions
{
    public const string SectionName = "Farol";

    /// <summary>Permite cadastro sem autenticação. Desligar fecha o formulário.</summary>
    public bool PublicRegistrationEnabled { get; set; } = true;

    /// <summary>Máximo de sites monitorados ao mesmo tempo, somando todos.</summary>
    public int MaxActiveSites { get; set; } = 25;

    /// <summary>Intervalo mínimo aceito no cadastro.</summary>
    public int MinCheckIntervalMinutes { get; set; } = 15;

    /// <summary>Validade de um site cadastrado publicamente.</summary>
    public int DemoSiteLifetimeHours { get; set; } = 24;

    /// <summary>
    /// Teto de checagens por ciclo do worker. É a única defesa que não confia
    /// em nada: mesmo com o banco cheio, o custo de um ciclo é este.
    /// </summary>
    public int MaxChecksPerCycle { get; set; } = 20;

    /// <summary>Por quantos dias o histórico é mantido.</summary>
    public int CheckRetentionDays { get; set; } = 7;
}
