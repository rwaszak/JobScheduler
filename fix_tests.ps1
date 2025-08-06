# Script to systematically fix test files by removing DailyBatch references
# and updating them to use only ContainerAppHealth or test job names

$testFiles = @(
    "test\JobScheduler.FunctionApp.Tests\DebugTests\ValidationDebugTests.cs",
    "test\JobScheduler.FunctionApp.Tests\IntegrationTests\ConfigurationBindingTests.cs", 
    "test\JobScheduler.FunctionApp.Tests\IntegrationTests\JobNameValidationIntegrationTests.cs",
    "test\JobScheduler.FunctionApp.Tests\IntegrationTests\RealConfigurationTests.cs",
    "test\JobScheduler.FunctionApp.Tests\UnitTests\JobConfigurationProviderTests.cs",
    "test\JobScheduler.FunctionApp.Tests\UnitTests\ValidateJobSchedulerOptionsTests.cs"
)

foreach ($file in $testFiles) {
    if (Test-Path $file) {
        Write-Host "Processing: $file"
        $content = Get-Content $file -Raw
        
        # Replace JobNames.DailyBatch with a test string or remove lines
        $content = $content -replace 'JobNames\.DailyBatch', '"test-job"'
        
        # Save the updated content
        Set-Content -Path $file -Value $content -NoNewline
        Write-Host "Updated: $file"
    }
}

Write-Host "All test files processed"
