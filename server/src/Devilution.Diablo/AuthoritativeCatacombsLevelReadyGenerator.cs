namespace Devilution.Diablo;

/** Catacombs-specific public facade for the shared pre-join ready phase. */
public static class AuthoritativeCatacombsLevelReadyGenerator
{
	public static AuthoritativeCatacombsLevelReadyResult Generate(
		AuthoritativeCatacombsLevelReadyRequest request)
		=> AuthoritativeCathedralLevelReadyGenerator.GenerateCatacombs(request);
}
