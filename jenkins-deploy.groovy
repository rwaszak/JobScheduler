// Deploys an existing build of the JobScheduler Functions to Azure Functions or Container Apps
pipeline {
    agent any

    parameters {
        choice(name: 'ENVIRONMENT', choices: ['dev', 'sit', 'uat', 'prod'], description: 'Select the environment')
        string(name: 'BUILD_NUMBER', defaultValue: '', description: 'Build number to deploy; leave blank for last successful build')
    }

    environment {
        GITHUB_SSH_URL = 'https://github.com/rwaszak/JobScheduler.git'
        AZURE_CONFIG_DIR = '/var/lib/jenkins/.azure-cli'
        DOCKER_REGISTRY_NAME = 'continuumaicontainers' // Update this to your ACR name
        DOCKER_IMAGE_NAME = 'jobscheduler-functions'
        BUILD_PIPELINE_NAME = 'JobScheduler Functions - Build' // Update with your build pipeline name
    }

    stages {
        stage('Get Build Info') {
            steps {
                script {
                    def buildSelector
                    if (params.BUILD_NUMBER?.trim()) {
                        // If a specific build number was provided, use that
                        buildSelector = specific(params.BUILD_NUMBER)
                        env.BUILD_NUMBER = params.BUILD_NUMBER
                        echo "Using specific build: #${env.BUILD_NUMBER}"
                    } else {
                        // Otherwise use last successful build
                        buildSelector = lastSuccessful()
                        echo "Using last successful build"

                        def job = Jenkins.instance.getItemByFullName(env.BUILD_PIPELINE_NAME)
                        if (!job) {
                            error "Could not find job: ${env.BUILD_PIPELINE_NAME}"
                        }
                    }

                    copyArtifacts(
                        projectName: "${env.BUILD_PIPELINE_NAME}",
                        selector: buildSelector,
                        filter: 'build-info.properties'
                    )

                    if (fileExists('build-info.properties')) {
                        def propsContent = readFile(file: 'build-info.properties')

                        propsContent.split('\n').each { line ->
                            if (line.startsWith('IMAGE_TAG=')) {
                                env.BUILD_VERSION = line.substring('IMAGE_TAG='.length()).trim()
                            } else if (line.startsWith('BUILD_NUMBER=')) {
                                env.BUILD_NUMBER = line.substring('BUILD_NUMBER='.length()).trim()
                            } else if (line.startsWith('BRANCH_NAME=')) {
                                env.BRANCH_NAME = line.substring('BRANCH_NAME='.length()).trim()
                            }
                        }

                        echo "Deploying build number: #${env.BUILD_NUMBER}, version: ${env.BUILD_VERSION}, branch: ${env.BRANCH_NAME}"

                    } else {
                        error "No build-info.properties file found in selected build"
                    }
                }
            }
        }

        stage('Checkout') {
            steps {
                script {
                    cleanWs()

                    echo "Checking out branch: ${env.BRANCH_NAME}"

                    checkout([$class: 'GitSCM',
                            branches: [[name: "*/${env.BRANCH_NAME}"]],
                            userRemoteConfigs: [[url: env.GITHUB_SSH_URL, credentialsId: 'github-personal-access-token-dg']]])

                    echo "Selected image tag for deployment: ${env.BUILD_VERSION}"
                }
            }
        }

        stage('Deploy') {
            when {
                allOf {
                    expression { currentBuild.resultIsBetterOrEqualTo('SUCCESS') }
                }
            }
            steps {
                script {
                    // Deploy steps and container config are in the functions repo
                    def deploy = load 'deploy.groovy'
                    deploy.call([
                        environment: params.ENVIRONMENT,
                        buildVersion: env.BUILD_VERSION,
                        buildNumber: env.BUILD_NUMBER
                    ])
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
                    Environment: ${params.ENVIRONMENT}
                    Branch: ${env.BRANCH_NAME}
                    Image Tag: ${env.BUILD_VERSION}
                    Triggered by: ${buildUser}

                    View build details at:
                    ${BUILD_URL}
                    """,
                    subject: "JobScheduler Functions Deployed - ${params.ENVIRONMENT} - Build #${BUILD_NUMBER} - ${currentBuild.result}",
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
                    message: "${slackEmoji} *JobScheduler Functions Deployed ${currentBuild.result}*\n" +
                            "Environment: ${params.ENVIRONMENT}\n" +
                            "Branch: ${env.BRANCH_NAME}\n" +
                            "Image Tag: ${env.BUILD_VERSION}\n" +
                            "Triggered by: ${buildUser}\n" +
                            "Build #${BUILD_NUMBER} - <${BUILD_URL}|View Details>"
                )
            }

            cleanWs()
        }
        unstable {
            echo "Deploy marked as UNSTABLE due to configuration issues"
        }
        failure {
            echo "Deploy FAILED due to unexpected errors"
        }
        success {
            echo "Deploy completed successfully"
        }
    }
}
