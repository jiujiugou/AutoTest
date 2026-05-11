import api from './http'

export const TestPlanApi = {
  list(take = 50) {
    return api.get(`/api/testplan/list?take=${take}`)
  },
  get(id) {
    return api.get(`/api/testplan/${encodeURIComponent(id)}`)
  },
  create(data) {
    return api.post('/api/testplan', data)
  },
  update(id, data) {
    return api.put(`/api/testplan/${encodeURIComponent(id)}`, data)
  },
  remove(id) {
    return api.delete(`/api/testplan/${encodeURIComponent(id)}`)
  },
  run(id) {
    return api.post(`/api/testplan/${encodeURIComponent(id)}/run`)
  },
  getReport(id, planRunId) {
    return api.get(`/api/testplan/${encodeURIComponent(id)}/report?planRunId=${encodeURIComponent(planRunId)}`)
  },
  getRuns(id, take = 20) {
    return api.get(`/api/testplan/${encodeURIComponent(id)}/runs?take=${take}`)
  }
}
