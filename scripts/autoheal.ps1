#usage: .\scripts\autoheal.ps1 -SolutionFile .\Blumchen.sln -ComposeFile .\docker-compose.yml
param(
  [string]$SolutionFile,
  [string]$ComposeFile
)


$env:DOCKER_CLI_HINTS=$false #disable docker hints
Write-Host "Setup infrastrucure"

try { 

	start powershell {
		docker compose up;
		Read-Host;
		
	} 
					
	Write-Host "Waiting for container readiness..."
	do
	{
		Start-Sleep -s 5
		$state=$(docker inspect db|ConvertFrom-Json).State
		$status=$state.Status
		$exitCode=$state.ExitCode
		$restart=$state.Restarting
	}Until(($status -eq "running") -and ($exitCode -eq 0) -and ($restart -eq $false))

	Write-Host "...Done"
	
	Write-Host "Start subscriber"
	start powershell {
		dotnet run --project ./src/SubscriberWorker/SubscriberWorker.csproj
		Read-Host;
	}

	Write-Host "Publishing 10 messages to test the subscriptions are working properly: hit ENTER when done!"
		
	start powershell {
		dotnet run --project ./src/Publisher/Publisher.csproj -- -c 10 -t \"UserCreated|UserDeleted|UserModified\";
	}

	Read-Host;

	Write-Host "Start massive insert to force wal segment creation..."
	start powershell {
		dotnet run --project ./src/Publisher/Publisher.csproj -- -c 800000 -t "UserSubscribed"
	}

	Write-Host "Wait for subscribers to auto heal on error...reporting on row insert"
	
	Start-Sleep -s 15
	do
	{
		docker exec -it db psql -h localhost -U postgres -w -c "select count(*) from outbox;"
	}Until(Read-Host "Enter to report on counting rows(another key to proceed when done)" "")

	Write-Host "Subscribers resiliency tested :-)"
	Write-Host "Publishing 10 messages to test the subscriptions are still working properly: hit ENTER when done!"

	start powershell {
		dotnet run --project ./src/Publisher/Publisher.csproj -- -c 10 -t \"UserCreated|UserDeleted|UserModified\"
	}
	Read-Host;
	
	Write-Host "We're done...: hit ENTER to shut down!"
	
	Read-Host;

}catch {
  Write-Host "An error occurred:"
  Write-Host $_
}
finally{
	docker compose -f $ComposeFile down --rmi local 
}
