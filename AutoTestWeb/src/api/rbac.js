// api/rbac.js
import api from "./http";

// ================= 角色 =================
export const rbacRoles = () =>
  api.get("/api/rbac/roles");

// ================= 权限 =================
export const rbacPermissions = () =>
  api.get("/api/rbac/permissions");

// ================= 角色权限 =================
export const rbacRolePermissions = (roleId) =>
  api.get(`/api/rbac/roles/${roleId}/permissions`);

// ================= 设置角色权限 =================
export const rbacSetRolePermissions = (roleId, codes) =>
  api.put(`/api/rbac/roles/${roleId}/permissions`, {
    codes
  });

// ================= 用户 =================
export const rbacUsers = (take = 100) =>
  api.get(`/api/rbac/users?take=${take}`);

// ================= 用户角色 =================
export const rbacUserRole = (userId) =>
  api.get(`/api/rbac/users/${userId}/role`);

// ================= 设置用户角色 =================
export const rbacSetUserRole = (userId, roleName) =>
  api.put(`/api/rbac/users/${userId}/role`, {
    roleName
  });
