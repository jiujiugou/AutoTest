import api from "./http";

export const MonitorsApi = {
  list() {
    return api.get("/api/monitor/list");
  },

  get(id) {
    return api.get(`/api/monitor/${id}`);
  },

  create(data) {
    return api.post("/api/monitor", data);
  },

  update(id, data) {
    return api.put(`/api/monitor/${id}`, data);
  },

  setEnabled(id, isEnabled) {
    return api.put(`/api/monitor/${id}/enabled`, { isEnabled: !!isEnabled });
  },

  remove(id) {
    return api.delete(`/api/monitor/${id}`);
  },

  run(id) {
    return api.post(`/api/monitor/${id}/run`, "");
  },

  latestExecution(id) {
    return api.get(`/api/monitor/${id}/executions/latest`);
  },

  executions(id, take = 20) {
    return api.get(`/api/monitor/${id}/executions?take=${encodeURIComponent(String(take))}`);
  },

  runtimeStats(id, topErrors = 10) {
    return api.get(`/api/monitor/${id}/runtime-stats?topErrors=${encodeURIComponent(String(topErrors))}`);
  },

  executionAssertions(executionId) {
    return api.get(`/api/monitor/executions/${encodeURIComponent(String(executionId))}/assertions`);
  }
};
