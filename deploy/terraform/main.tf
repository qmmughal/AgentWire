provider "aws" {
  region = var.aws_region
}

variable "aws_region" {
  default = "us-east-1"
}

variable "cluster_name" {
  default = "agentwire-cluster"
}

module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "5.0.0"

  name = "agentwire-vpc"
  cidr = "10.0.0.0/16"

  azs             = ["us-east-1a", "us-east-1b", "us-east-1c"]
  private_subnets = ["10.0.1.0/24", "10.0.2.0/24", "10.0.3.0/24"]
  public_subnets  = ["10.0.101.0/24", "10.0.102.0/24", "10.0.103.0/24"]

  enable_nat_gateway = true
  single_nat_gateway = true
}

module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "19.15.3"

  cluster_name    = var.cluster_name
  cluster_version = "1.27"
  
  vpc_id                   = module.vpc.vpc_id
  subnet_ids               = module.vpc.private_subnets
  cluster_endpoint_public_access = true

  eks_managed_node_groups = {
    agentwire_nodes = {
      min_size     = 2
      max_size     = 5
      desired_size = 3

      instance_types = ["t3.large"]
      capacity_type  = "ON_DEMAND"
    }
  }
}

resource "aws_db_instance" "postgres" {
  identifier           = "agentwire-db"
  engine               = "postgres"
  engine_version       = "15.3"
  instance_class       = "db.t3.medium"
  allocated_storage    = 50
  db_name              = "agentwire"
  username             = "agentwireadmin"
  password             = "ChangeMe123!" # In production, use secrets manager
  skip_final_snapshot  = true
  vpc_security_group_ids = [module.eks.node_security_group_id]
  db_subnet_group_name   = module.vpc.database_subnet_group_name
}
