# 🔐 EFS with KMS Encryption on Amazon EKS

This guide provisions an Amazon EFS (Elastic File System) volume encrypted with a **Customer Managed Key (CMK)** using AWS KMS, and mounts it into an **EKS** cluster via the **EFS CSI driver**.

---

## ✅ Objective

Provision an encrypted EFS volume with a customer-managed KMS key, mount it using the EFS CSI driver in an EKS cluster, and allow pods to access it via PVCs.

---

## 🧩 Prerequisites

- Existing EKS cluster.
- AWS CLI, `eksctl`, and `kubectl` installed.
- A KMS CMK (Customer Managed Key) ARN.
- IAM role or user with permission to:
  - Create IAM roles and policies
  - Use EFS and KMS
  - Deploy to EKS

---
## 📌 Example Customer Managed Key (CMK) ARN

```
arn:aws:kms:us-east-1:123456789012:key/abcd1234-5678-90ef-ghij-klmnopqrstuv
```

---

## 1️⃣ Create IAM Policy for KMS Access

Save the following content as `efs-kms-policy.json`:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "kms:CreateGrant",
        "kms:ListGrants",
        "kms:RevokeGrant",
        "kms:Encrypt",
        "kms:Decrypt",
        "kms:ReEncrypt*",
        "kms:GenerateDataKey*",
        "kms:DescribeKey"
      ],
      "Resource": "arn:aws:kms:us-east-1:123456789012:key/abcd1234-5678-90ef-ghij-klmnopqrstuv"
    }
  ]
}
```

Create the policy:

```bash
aws iam create-policy \
  --policy-name KMS_EFS_Encryption_Policy \
  --policy-document file://efs-kms-policy.json
```

---

## 2️⃣ Create IAM Service Account for EFS CSI Driver

```bash
export CLUSTER_NAME=your-cluster-name
export REGION=us-east-1
export ROLE_NAME=AmazonEKS_EFS_CSI_DriverRole

eksctl create iamserviceaccount \
  --name efs-csi-controller-sa \
  --namespace kube-system \
  --cluster $CLUSTER_NAME \
  --region $REGION \
  --role-name $ROLE_NAME \
  --role-only \
  --attach-policy-arn arn:aws:iam::aws:policy/service-role/AmazonEFSCSIDriverPolicy \
  --attach-policy-arn arn:aws:iam::123456789012:policy/KMS_EFS_Encryption_Policy \
  --approve
```

---

## 3️⃣ Install the EFS CSI Driver

```bash
eksctl create addon \
  --name aws-efs-csi-driver \
  --cluster $CLUSTER_NAME \
  --region $REGION \
  --service-account-role-arn arn:aws:iam::123456789012:role/$ROLE_NAME \
  --force
```

---

## 4️⃣ Create EFS File System (Encrypted)

```bash
aws efs create-file-system \
  --encrypted \
  --kms-key-id arn:aws:kms:us-east-1:123456789012:key/abcd1234-5678-90ef-ghij-klmnopqrstuv \
  --performance-mode generalPurpose \
  --throughput-mode bursting \
  --tags Key=Name,Value=my-efs
```

📌 **Save the File System ID** (e.g., `fs-12345678`)

---

## 5️⃣ Create Mount Targets (Repeat for Each Subnet)

```bash
aws efs create-mount-target \
  --file-system-id fs-12345678 \
  --subnet-id subnet-xxxxxxxx \
  --security-groups sg-xxxxxxxx
```

---

## 6️⃣ Create StorageClass, PersistentVolume, and PersistentVolumeClaim

### `efs-sc.yaml`

```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: efs-sc
provisioner: efs.csi.aws.com
```

### `efs-pv-pvc.yaml`

```yaml
apiVersion: v1
kind: PersistentVolume
metadata:
  name: efs-pv
spec:
  capacity:
    storage: 5Gi
  volumeMode: Filesystem
  accessModes:
    - ReadWriteMany
  persistentVolumeReclaimPolicy: Retain
  storageClassName: efs-sc
  csi:
    driver: efs.csi.aws.com
    volumeHandle: fs-12345678
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: efs-pvc
spec:
  accessModes:
    - ReadWriteMany
  storageClassName: efs-sc
  resources:
    requests:
      storage: 5Gi
```

Apply the configs:

```bash
kubectl apply -f efs-sc.yaml
kubectl apply -f efs-pv-pvc.yaml
```

---

## 7️⃣ Deploy a Test Pod Using EFS

### `app-using-efs.yaml`

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: app-using-efs
spec:
  containers:
  - name: app
    image: amazonlinux
    command: ["sleep", "9999999"]
    volumeMounts:
    - name: efs-vol
      mountPath: /data
  volumes:
  - name: efs-vol
    persistentVolumeClaim:
      claimName: efs-pvc
```

Deploy the pod:

```bash
kubectl apply -f app-using-efs.yaml
```

---

## 🔐 Final Notes

✅ EFS is encrypted at rest using your KMS Customer Managed Key.  
✅ The EFS CSI driver IAM role has permission to use KMS operations like Encrypt, Decrypt, and GenerateDataKey.  
✅ Storage is shared across pods using `ReadWriteMany` access mode with the `efs-pvc`.
