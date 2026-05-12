using '../main.bicep'

param environment = 'dev'
param prefix = 'azlearn'
param batchProcessorImage = 'azlearnacrdev.azurecr.io/batchprocessor-api:dev'
param progressReceiverImage = 'azlearnacrdev.azurecr.io/progressreceiver-api:dev'
param cpuCores = '0.5'
param memoryGi = '1Gi'
param minReplicas = 1
param maxReplicas = 3
