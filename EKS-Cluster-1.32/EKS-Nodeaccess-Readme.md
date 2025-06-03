# 📘 Amazon EKS Access Control via `aws-auth` ConfigMap

Amazon EKS uses the `aws-auth` ConfigMap to map AWS IAM users and roles to Kubernetes RBAC permissions. This is essential for granting access to EKS cluster resources beyond the initial creator.

> ⚠️ By default, only the IAM identity that created the EKS cluster has `system:masters` (admin) access. Additional users and roles must be explicitly added.

---

## 🔧 What This File Does

This configuration:
- Allows **EKS worker nodes** (EC2 instances) to join the cluster
- Grants **IAM users or roles** access to the Kubernetes API (including admin access if specified)

---

## 📁 Example `aws-auth.yaml`

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: aws-auth
  namespace: kube-system
data:
  mapRoles: |
    # Worker node group role 1
    - rolearn: arn:aws:iam::730335280129:role/eksctl-VotingAPP-Prod-Private-node-NodeInstanceRole-rpMM6QII0p9l
      username: system:node:{{EC2PrivateDNSName}}
      groups:
        - system:bootstrappers
        - system:nodes

    # Worker node group role 2
    - rolearn: arn:aws:iam::730335280129:role/eksctl-VotingAPP-Prod-Private-node-NodeInstanceRole-KOTI27UYUcAr
      username: system:node:{{EC2PrivateDNSName}}
      groups:
        - system:bootstrappers
        - system:nodes

    # IAM role with full admin access
    - rolearn: arn:aws:iam::730335280129:role/AdminRole
      username: admin-role
      groups:
        - system:masters

  mapUsers: |
    # IAM user with full admin access
    - userarn: arn:aws:iam::730335280129:user/Test
      username: Test
      groups:
        - system:masters
```
## 🧠 Explanation

### `mapRoles`

**Used for:**

- EC2 node instance roles (to allow them to join the cluster)
- IAM roles (e.g., admin or CI/CD roles)

**Fields:**

- `rolearn`: Full IAM Role ARN
- `username`: Logical username in Kubernetes
- `groups`: Kubernetes RBAC groups (e.g., `system:nodes`, `system:masters`)

---

### `mapUsers`

**Used to grant access to individual IAM users.**

**Fields:**

- `userarn`: Full IAM User ARN
- `username`: Logical username in Kubernetes
- `groups`: Kubernetes RBAC groups (e.g., `system:masters`)

---

## 🚀 Apply the Config

Save the YAML file and apply it:

```bash
kubectl apply -f aws-auth.yaml
```
Verify it was applied:

```bash
kubectl get configmap aws-auth -n kube-system -o yaml
```

## ✅ Test Access

Assume the role or log in as the IAM user, then run:

```bash
kubectl get nodes
```
If access is correctly configured, the user or role should see the cluster resources without error.

##⚠️ Notes

    Do not remove the original creator's access until new access is verified.

    system:masters provides full cluster admin privileges.
