// Builds JobScheduler Azure Functions App (.NET) and publishes to the container registry; does NOT deploy
pipeline {
    agent any

    parameters {
        string(name: 'BRANCH', defaultValue: 'main', description: 'Specify a branch name (e.g., main, jenkins, feature/CON-123-Some-Feature)')
    }

    environment {
        GITHUB_SSH_URL = 'https://github.com/rwaszak/JobScheduler.git'
        AZURE_CONFIG_DIR = '/var/lib/jenkins/.azure-cli'
        DOCKER_REGISTRY_NAME = 'continuumaicontainers' // Update this to your ACR name
        DOCKER_IMAGE_NAME = 'jobscheduler-functions'
        BUILD_VERSION = "1.0.0.${env.BUILD_NUMBER}"
        PROJECT_PATH = 'src/JobScheduler.FunctionApp'
    }

    stages {
        stage('Checkout') {
            steps {
                script {
                    def branchName = params.BRANCH

                    cleanWs()

                    checkout([$class: 'GitSCM',
                              branches: [[name: "*/${branchName}"]],
                              userRemoteConfigs: [[url: env.GITHUB_SSH_URL, credentialsId: 'github-personal-access-token-dg']]])

                    // Print current branch
                    sh "git branch --show-current"

                    env.ACTUAL_BRANCH = branchName

                    // Normalize branch name for Docker tag (replace slashes and other invalid chars)
                    env.SAFE_BRANCH_NAME = env.ACTUAL_BRANCH.replaceAll('/', '-').replaceAll(' ', '-').toLowerCase()

                    // Combine build version and branch for the complete tag
                    env.IMAGE_TAG = "${env.BUILD_VERSION}-${env.SAFE_BRANCH_NAME}"

                    echo "Using branch name: ${env.ACTUAL_BRANCH}"
                    echo "Safe branch name for tagging: ${env.SAFE_BRANCH_NAME}"
                    echo "Image tag will be: ${env.IMAGE_TAG}"
                }
            }
        }

        stage('Setup .NET') {
            steps {
                sh '''
                    wget https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh
                    chmod +x dotnet-install.sh

                    # Install .NET SDK 8.0 (includes both runtime and SDK)
                    sudo ./dotnet-install.sh --channel 8.0 --runtime dotnet --version 8.0.13 --install-dir /usr/share/dotnet

                    # Also install the specific ASP.NET Core 8.0.0 runtime required by testhost
                    sudo ./dotnet-install.sh --channel 8.0 --runtime aspnetcore --version 8.0.0 --install-dir /usr/share/dotnet

                    # Verify installation
                    dotnet --info
                '''
            }
        }

        stage('Build and Test') {
            steps {
                withCredentials([
                    string(credentialsId: 'datadog-api-key', variable: 'DD_API_KEY'),
                    string(credentialsId: 'azure-storage-connection-string-dev', variable: 'AZURE_STORAGE_CONNECTION_STRING'),
                ]) {

                    sh """
                        # Navigate to the Functions project directory
                        cd ${env.PROJECT_PATH}
                        
                        dotnet restore
                        dotnet build --configuration Release

                        dotnet --list-runtimes

                        mkdir -p ../../TestResults

                        # Set environment variables for Functions testing
                        export DD_API_KEY="${DD_API_KEY}"
                        export AzureWebJobsStorage="${AZURE_STORAGE_CONNECTION_STRING}"
                        export FUNCTIONS_WORKER_RUNTIME="dotnet-isolated"
                        
                        # Run tests from the test project
                        cd ../../test/JobScheduler.FunctionApp.Tests
                        dotnet test --configuration Release --no-build \
                        --logger "console;verbosity=normal" \
                        --logger "trx" \
                        --results-directory ../../TestResults

                        # Show what files were created
                        ls -la ../../TestResults/
                    """
                }

                archiveArtifacts artifacts: '**/bin/Release/net8.0/*.dll', fingerprint: true
                archiveArtifacts artifacts: '**/TestResults/**/*.trx', allowEmptyArchive: false
            }
            post {
                always {
                    mstest testResultsFile: '**/TestResults/*.trx', failOnError: true
                }
                failure {
                    script {
                        currentBuild.result = 'FAILURE'
                    }
                }
            }
        }

        stage('Publish Functions App') {
            when {
                allOf {
                    expression { currentBuild.resultIsBetterOrEqualTo('SUCCESS') }
                }
            }
            steps {
                sh """
                    cd ${env.PROJECT_PATH}
                    dotnet publish --configuration Release --output bin/Release/net8.0/publish
                """
            }
        }

        stage('Build and Push Docker Image') {
            when {
                allOf {
                    expression { currentBuild.resultIsBetterOrEqualTo('SUCCESS') }
                }
            }
            steps {
                withCredentials([azureServicePrincipal('jenkins-service-principal-2')]) {
                    sh """
                        az login --service-principal -u $AZURE_CLIENT_ID -p $AZURE_CLIENT_SECRET -t $AZURE_TENANT_ID
                        az account set --subscription $AZURE_SUBSCRIPTION_ID

                        # Build the Docker image
                        sudo docker build --build-arg VERSION=${env.BUILD_VERSION} -t ${env.DOCKER_IMAGE_NAME}:${env.IMAGE_TAG} .

                        # Login to Azure Container Registry
                        az acr login --name ${env.DOCKER_REGISTRY_NAME}

                        # Tag and push the image
                        docker tag ${env.DOCKER_IMAGE_NAME}:${env.IMAGE_TAG} ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${env.IMAGE_TAG}
                        docker push ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${env.IMAGE_TAG}

                        az logout
                    """
                }
            }
        }

        stage('Save Build Info') {
            steps {
                script {
                    // Save build info to be accessed by the deploy pipeline
                    def propsContent = """
# Build information generated by Jenkins
BUILD_TIMESTAMP=${new Date().format("yyyy-MM-dd HH:mm:ss")}
IMAGE_TAG=${env.IMAGE_TAG}
BRANCH_NAME=${env.ACTUAL_BRANCH}
BUILD_NUMBER=${env.BUILD_NUMBER}
VERSION=${env.BUILD_VERSION}
"""

                    writeFile file: 'build-info.properties', text: propsContent
                    archiveArtifacts artifacts: 'build-info.properties', fingerprint: true
                    echo "Build information saved as Jenkins artifact"
                }
            }
        }
    }

    post {
        always {
            script {
                def buildUser = ""
                wrap([$class: 'BuildUser']) {
                    buildUser = "${BUILD_USER_FIRST_NAME} ${BUILD_USER_LAST_NAME}"
                }

                emailext body: """
                    Build Status: ${currentBuild.result}
                    Branch: ${params.BRANCH}
                    Version: ${env.BUILD_VERSION}
                    Image Tag: ${env.IMAGE_TAG ?: 'No image created'}
                    Triggered by: ${buildUser}

                    View build details at:
                    ${BUILD_URL}
                    """,
                    subject: "JobScheduler Functions - ${env.IMAGE_TAG ?: params.BRANCH} - Build #${BUILD_NUMBER} - ${currentBuild.result}",
                    to: '${DEFAULT_RECIPIENTS}',
                    attachLog: false

                // Slack notification
                def slackColor = currentBuild.result == 'SUCCESS' ? 'good' :
                               currentBuild.result == 'UNSTABLE' ? 'warning' : 'danger'
                def slackEmoji = currentBuild.result == 'SUCCESS' ? '✅' :
                               currentBuild.result == 'UNSTABLE' ? '⚠️' : '❌'

                slackSend(
                    channel: '#jenkins-notifications',
                    color: slackColor,
                    message: "${slackEmoji} *JobScheduler Functions Build ${currentBuild.result}*\n" +
                            "Branch: ${params.BRANCH}\n" +
                            "Version: ${env.BUILD_VERSION}\n" +
                            "Image Tag: ${env.IMAGE_TAG ?: 'No image created'}\n" +
                            "Triggered by: ${buildUser}\n" +
                            "Build #${BUILD_NUMBER} - <${BUILD_URL}|View Details>"
                )
            }

            cleanWs()
        }
        unstable {
            echo "Build marked as UNSTABLE due to configuration issues"
        }
        failure {
            echo "Build FAILED due to unexpected errors"
        }
        success {
            echo "Build completed successfully"
        }
    }
}
