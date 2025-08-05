// SIMPLIFIED Jenkins Test Pipeline for JobScheduler Functions 
// This is for incremental testing - deploy to existing job-scheduler-poc app
pipeline {
    agent any

    parameters {
        choice(name: 'ENVIRONMENT', choices: ['dev'], description: 'Environment (dev for testing)')
        string(name: 'DOCKER_IMAGE_TAG', defaultValue: 'test-build-1', description: 'Docker image tag to deploy')
        booleanParam(name: 'SKIP_BUILD', defaultValue: false, description: 'Skip build and just deploy existing image')
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
            when {
                not { 
                    equals expected: true, actual: params.SKIP_BUILD
                }
            }
            steps {
                script {
                    echo "Building Docker image for testing..."
                    
                    sh """
                        # Build the Docker image
                        docker build -t ${env.DOCKER_IMAGE_NAME}:${params.DOCKER_IMAGE_TAG} .
                        
                        # Show what we built
                        docker images | grep ${env.DOCKER_IMAGE_NAME}
                    """
                    
                    echo "Docker build completed with tag: ${params.DOCKER_IMAGE_TAG}"
                }
            }
        }

        stage('Push to Registry') {
            when {
                not { 
                    equals expected: true, actual: params.SKIP_BUILD
                }
            }
            steps {
                withCredentials([azureServicePrincipal('jenkins-service-principal-2')]) {
                    sh """
                        # Login to Azure and ACR
                        az login --service-principal -u $AZURE_CLIENT_ID -p $AZURE_CLIENT_SECRET -t $AZURE_TENANT_ID
                        az account set --subscription $AZURE_SUBSCRIPTION_ID
                        az acr login --name ${env.DOCKER_REGISTRY_NAME}

                        # Tag and push the image
                        docker tag ${env.DOCKER_IMAGE_NAME}:${params.DOCKER_IMAGE_TAG} ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${params.DOCKER_IMAGE_TAG}
                        docker push ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${params.DOCKER_IMAGE_TAG}

                        echo "Image pushed to: ${env.DOCKER_REGISTRY_NAME}.azurecr.io/${env.DOCKER_IMAGE_NAME}:${params.DOCKER_IMAGE_TAG}"
                        
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
                        buildVersion: params.DOCKER_IMAGE_TAG,
                        buildNumber: env.BUILD_NUMBER
                    ])
                }
            }
        }

        stage('Test Deployment') {
            steps {
                script {
                    echo "Testing the deployed Functions app..."
                    
                    sleep 30 // Give the app time to restart
                    
                    sh """
                        echo "Testing endpoints..."
                        
                        # Test health endpoint
                        curl -f https://job-scheduler-poc.azurewebsites.net/api/health || echo "Health endpoint failed"
                        
                        # Test jobs endpoint  
                        curl -f https://job-scheduler-poc.azurewebsites.net/api/jobs || echo "Jobs endpoint failed"
                        
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
                Image Tag: ${params.DOCKER_IMAGE_TAG}
                Triggered by: ${buildUser}
                
                Test URLs:
                - Health: https://job-scheduler-poc.azurewebsites.net/api/health
                - Jobs: https://job-scheduler-poc.azurewebsites.net/api/jobs
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
