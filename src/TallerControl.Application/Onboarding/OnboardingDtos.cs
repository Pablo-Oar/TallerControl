namespace TallerControl.Application.Onboarding;

public sealed record OnboardingStepDto(string Key, string Label, bool Completed);

public sealed record OnboardingDto(
    IReadOnlyList<OnboardingStepDto> Steps,
    int CompletedCount,
    int TotalCount,
    bool IsComplete);
