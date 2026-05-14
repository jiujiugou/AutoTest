import api from "./http";

export const adminUsers = (take = 200) =>
  api.get(`/api/admin/users?take=${take}`);

export const adminCreateUser = (data) =>
  api.post("/api/admin/users", data);

export const adminUpdateUser = (id, data) =>
  api.put(`/api/admin/users/${id}`, data);

export const adminDeleteUser = (id) =>
  api.delete(`/api/admin/users/${id}`);

export const adminResetPassword = (id, password) =>
  api.put(`/api/admin/users/${id}/password`, { password });
