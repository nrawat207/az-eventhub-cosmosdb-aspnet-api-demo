using '../main.bicep'

param environment = 'dev'
param prefix = 'azlearn'
param batchProcessorImage = 'azlearnacrdev.azurecr.io/batchprocessor-api:dev'
param progressReceiverImage = 'azlearnacrdev.azurecr.io/progressreceiver-api:dev'
param batchProcessorAppName = 'batch-processor-api'
param progressReceiverAppName = 'progress-receiver-api'
param cpuCores = '0.5'
param memoryGi = '1Gi'
param minReplicas = 1
param maxReplicas = 3
param deployContainerApps = true
