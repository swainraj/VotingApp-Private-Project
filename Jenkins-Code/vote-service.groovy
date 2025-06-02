pipeline {
  agent any

  environment {
    ECR_REPO = "730335280129.dkr.ecr.us-east-1.amazonaws.com/vote-ecr"
    IMAGE_TAG = "latest"
  }

  stages {
    stage('Checkout Source') {
      steps {
        git credentialsId: 'GITHUB', url: 'https://github.com/techrajes/vote-service.git'
      }
    }

    stage('Login to AWS ECR') {
      steps {
        withCredentials([[$class: 'AmazonWebServicesCredentialsBinding', credentialsId: 'aws-jenkins']]) {
          sh '''
            aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin $ECR_REPO
          '''
        }
      }
    }

    stage('Build Docker Image') {
      steps {
        sh 'docker build -t $ECR_REPO:$IMAGE_TAG .'
      }
    }

    stage('Push Docker Image') {
      steps {
        sh 'docker push $ECR_REPO:$IMAGE_TAG'
      }
    }

    stage('Deploy to EKS via Helm') {
      steps {
        withCredentials([file(credentialsId: 'kubeconfig-prod', variable: 'KUBECONFIG_FILE')]) {
          withEnv(["KUBECONFIG=$KUBECONFIG_FILE"]) {
            sh '''
              echo "Current working directory:"
              pwd
              echo "Listing files:"
              ls -R
              helm upgrade --install vote ./vote-chart \
                --set image.repository=730335280129.dkr.ecr.us-east-1.amazonaws.com/vote-ecr \
                --set image.tag=latest
            '''
          }
        }
      }
    }
  }

  post {
    failure {
      echo 'Build or deployment failed!'
    }
  }
}
