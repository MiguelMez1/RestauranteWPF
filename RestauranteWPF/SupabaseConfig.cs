namespace RestauranteWPF;

/// <summary>
/// Dados de conexão com o Supabase.
/// Onde encontrar: painel do Supabase > Project Settings > API.
///   - Url: "Project URL"
///   - ChaveAnon: chave pública "anon" (public)
/// Nunca coloque aqui a chave "service_role" (ela ignora todas as regras de segurança).
/// </summary>
public static class SupabaseConfig
{
    public const string Url = "https://mnpmjnfdskxhvsuxpyuq.supabase.co";
    public const string ChaveAnon = "sb_publishable_ptr7wnQmRIoey5YpXj0Nwg_BjbiBoyR";

    /// <summary>Indica se os valores acima ainda são os de exemplo.</summary>
    public static bool NaoConfigurado =>
        Url.Contains("SEU-PROJETO") || ChaveAnon.Contains("SUA-CHAVE");
}
