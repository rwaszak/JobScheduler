// SIMPLIFIED Jenkins Test Pipeline for JobScheduler Functions 
// This is for incremental testing - deploy to existing job-scheduler-poc app
pipeline {
    agent any

    parameters {
        choice(name: 'ENVIRONMENT', choices: ['dev'], description: 'Environment (dev for testing)')
        // Removed DOCKER_IMAGE_TAG parameter - now auto-generated
    }

    environment {
        GITHUB_SSH_URL = 'https://github.com/rwaszak/JobScheduler.git'
        DOCKER_REGISTRY_NAME = 'continuumaicontainers' // UPDATE THIS to your actual ACR name
        DOCKER_IMAGE_NAME = 'jobscheduler-functions'
        // Test mode settings
        USE_EXISTING_APP = 'true'  // Use existing job-scheduler-poc app
        DEPLOYMENT_METHOD = 'functions'
        PROJECT_PATH = 'src/JobScheduler.FunctionApp'
    }

    stages {
        stage('Checkout') {
            steps {
                script {
                    cleanWs()
                    
                    checkout([$class: 'GitSCM',
                              branches: [[name: "*/jenkins"]], // Use your jenkins branch
                              userRemoteConfigs: [[url: env.GITHUB_SSH_URL, credentialsId: 'github-personal-access-token-rw-job-scheduler']]])

                    echo "Checked out jenkins branch for testing"
                }
            }
        }

        stage('Build Docker Image') {
            steps {
                script {
                    // Generate unique image tag using build number and timestamp
                    def imageTag = "build-${env.BUILD_NUMBER}-${new Date().format('yyyyMMdd-HHmmss')}"
                    env.DOCKER_IMAGE_TAG = imageTag
                    
                    echo "Building Docker image for testing with unique tag: ${imageTag}"
                    
                    sh """
                        # Build the Docker image with tests (tests always run now)
                        echo "Building Docker image WITH tests..."
                        docker build -t ${env.DOCKER_IMAGE_NAME}:${imageTag} .
                        
                        # Show what we built
                        docker images | grep ${env.DOCKER_IMAGE_NAME}
                    """
                    
                    echo "Docker build completed with unique tag: ${imageTag}"
                }
            }
        }

        stage('Push to Registry') {
            steps {
                withCredentials([azureServicePrincipal('jenkins-service-principal-2')]) {
                    sh """
                        # Login to Azure and ACR
                        az login --service-principal -u $AZURE_CLIENT_ID -p $AZURE_CLIENT_SECRET -t $AZURE_TENANT_ID
                        az account set --subscription $AZURE_SUBSCRIPTION_ID
                        az acr login --name ${env.DOCKER_REGISTRY_NAME}

                        # Tag and push the image
                        docker tag ${env.DOCKER_IMAGE_NAME}:${env.DOCKER_IMAGE_TAG} ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${env.DOCKER_IMAGE_TAG}
                        docker push ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${env.DOCKER_IMAGE_TAG}

                        echo "Image pushed to: ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${env.DOCKER_IMAGE_TAG}"
                        
                        az logout
                    """
                }
            }
        }

        stage('Deploy to Existing Functions App') {
            steps {
                script {
                    echo "Deploying to existing job-scheduler-poc Functions app..."
                    
                    // Load and run the deploy script
                    def deploy = load 'deploy.groovy'
                    deploy.call([
                        environment: params.ENVIRONMENT,
                        buildVersion: env.DOCKER_IMAGE_TAG,
                        buildNumber: env.BUILD_NUMBER
                    ])
                }
            }
        }

        stage('Check Function App Status') {
            steps {
                script {
                    echo "Checking Function App deployment status..."
                    
                    withCredentials([azureServicePrincipal('jenkins-service-principal-2')]) {
                        sh """
                            # Login to Azure
                            az login --service-principal -u \$AZURE_CLIENT_ID -p \$AZURE_CLIENT_SECRET -t \$AZURE_TENANT_ID
                            az account set --subscription \$AZURE_SUBSCRIPTION_ID
                            
                            echo "=== Function App Status ==="
                            az functionapp show --name job-scheduler-poc-container --resource-group continuum_scheduled_jobs --query "{name:name,state:state,hostNames:hostNames[0],kind:kind}" -o table
                            
                            echo "=== Function App Logs (last 50 lines) ==="
                            az functionapp log tail --name job-scheduler-poc-container --resource-group continuum_scheduled_jobs --provider filesystem || echo "Could not retrieve logs"
                            
                            echo "=== Container Status ==="
                            az functionapp config container show --name job-scheduler-poc-container --resource-group continuum_scheduled_jobs || echo "Could not retrieve container config"
                            
                            az logout
                        """
                    }
                }
            }
        }

        stage('Test Deployment') {
            steps {
                script {
                    echo "Testing the deployed Functions app..."
                    
                    echo "Waiting for Function App to fully start up..."
                    sleep 60 // Increased wait time for container startup
                    
                    sh """
                        echo "Testing endpoints with retry logic..."
                        
                        # Function to test endpoint with retries
                        test_endpoint() {
                            local url=\$1
                            local name=\$2
                            local max_attempts=5
                            local wait_time=15
                            
                            for i in \$(seq 1 \$max_attempts); do
                                echo "Attempt \$i/\$max_attempts for \$name endpoint..."
                                if curl -f -m 30 "\$url"; then
                                    echo "\$name endpoint is working!"
                                    return 0
                                else
                                    echo "\$name endpoint failed (attempt \$i/\$max_attempts)"
                                    if [ \$i -lt \$max_attempts ]; then
                                        echo "Waiting \$wait_time seconds before retry..."
                                        sleep \$wait_time
                                    fi
                                fi
                            done
                            echo "❌ \$name endpoint failed after \$max_attempts attempts"
                            return 1
                        }
                        
                        # Test health endpoint with retries
                        test_endpoint "https://job-scheduler-poc-container.azurewebsites.net/api/health" "Health"
                        health_status=\$?
                        
                        # Test jobs endpoint with retries  
                        test_endpoint "https://job-scheduler-poc-container.azurewebsites.net/api/jobs" "Jobs"
                        jobs_status=\$?
                        
                        echo "=== Deployment Test Results ==="
                        if [ \$health_status -eq 0 ]; then
                            echo "✅ Health endpoint: PASSED"
                        else
                            echo "❌ Health endpoint: FAILED"
                        fi
                        
                        if [ \$jobs_status -eq 0 ]; then
                            echo "✅ Jobs endpoint: PASSED"
                        else
                            echo "❌ Jobs endpoint: FAILED"
                        fi
                        
                        echo "Deployment test completed!"
                    """
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

                echo """
                ===========================================
                JobScheduler Functions Test Deployment
                ===========================================
                Status: ${currentBuild.result}
                Environment: ${params.ENVIRONMENT}
                Image Tag: ${env.DOCKER_IMAGE_TAG}
                Triggered by: ${buildUser}
                
                Test URLs:
                - Health: https://job-scheduler-poc-container.azurewebsites.net/api/health
                - Jobs: https://job-scheduler-poc-container.azurewebsites.net/api/jobs
                ===========================================
                """
            }

            cleanWs()
        }
        success {
            echo "✅ Test deployment successful! Check the Functions app in Azure portal."
        }
        failure {
            echo "❌ Test deployment failed. Check logs above for details."
        }
    }
}
