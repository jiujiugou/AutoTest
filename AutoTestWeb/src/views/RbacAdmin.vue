<script setup>
import { computed, onMounted, ref } from "vue";
import { ElMessage } from 'element-plus'
import {
  rbacRoles,
  rbacPermissions,
  rbacRolePermissions,
  rbacSetRolePermissions,
  rbacUsers,
  rbacUserRole,
  rbacSetUserRole
} from "../api/rbac";

const busy = ref(false);
const error = ref("");

const roles = ref([]);
const permissions = ref([]);
const rolePermCodes = ref({});
const selectedRoleId = ref(null);

const users = ref([]);
const selectedUserId = ref(null);
const selectedUserRoleName = ref("");

/* ================= computed ================= */

const selectedRole = computed(() =>
  roles.value.find(r => r.id === selectedRoleId.value) || null
);

const roleCodes = computed(() => {
  const arr = rolePermCodes.value[String(selectedRoleId.value || "")] || [];
  return new Set(arr);
});

/* ================= utils ================= */

function setError(e) {
  error.value = e?.message ? e.message : String(e || "");
}

/* ================= load ================= */

async function loadAll() {
  busy.value = true;
  error.value = "";

  try {
    roles.value = await rbacRoles();
    permissions.value = await rbacPermissions();

    const results = await Promise.all(
      roles.value.map(r =>
        rbacRolePermissions(r.id).then(rows => ({
          id: r.id,
          rows
        }))
      )
    );

    const map = {};
    for (const item of results) {
      map[String(item.id)] = (item.rows || [])
        .map(x => (x.code || "").toLowerCase())
        .filter(Boolean);
    }

    rolePermCodes.value = map;

    if (!selectedRoleId.value && roles.value.length > 0) {
      selectedRoleId.value = roles.value[0].id;
    }

    users.value = await rbacUsers(200);

    if (!selectedUserId.value && users.value.length > 0) {
      selectedUserId.value = users.value[0].id;
      await loadUserRole();
    }

  } catch (e) {
    setError(e);
  } finally {
    busy.value = false;
  }
}

/* ================= role perms ================= */

function toggleRolePermission(code) {
  const rid = String(selectedRoleId.value || "");
  if (!rid) return;

  const c = String(code || "").toLowerCase();
  const arr = rolePermCodes.value[rid] || [];

  const i = arr.indexOf(c);
  if (i >= 0) arr.splice(i, 1);
  else arr.push(c);

  rolePermCodes.value[rid] = [...arr];
}

async function saveRolePermissions() {
  const rid = selectedRoleId.value;
  if (!rid) return;

  busy.value = true;
  error.value = "";

  try {
    const arr = rolePermCodes.value[String(rid)] || [];
    await rbacSetRolePermissions(rid, arr);

    alert("角色权限已保存");
  } catch (e) {
    setError(e);
  } finally {
    busy.value = false;
  }
}

/* ================= user role ================= */

async function loadUserRole() {
  if (!selectedUserId.value) return;

  try {
    const r = await rbacUserRole(selectedUserId.value);
    selectedUserRoleName.value = r?.name || "";
  } catch (e) {
    setError(e);
  }
}

async function saveUserRole() {
  if (!selectedUserId.value) return;

  busy.value = true;
  error.value = "";

  try {
    await rbacSetUserRole(selectedUserId.value, selectedUserRoleName.value);
    ElMessage.success("用户角色已保存");
  } catch (e) {
    setError(e);
  } finally {
    busy.value = false;
  }
}

onMounted(loadAll);
</script>

<template>
  <div class="rbac-container">
    <el-card class="main-card">
      <template #header>
        <div class="card-header">
          <span class="header-title">权限管理（RBAC）</span>
          <el-button type="primary" :icon="'Refresh'" :loading="busy" @click="loadAll">刷新</el-button>
        </div>
      </template>

      <el-alert
        v-if="error"
        :title="error"
        type="error"
        show-icon
        style="margin-bottom: 20px"
      />

      <el-row :gutter="24">
        <!-- ================= 角色权限 ================= -->
        <el-col :span="14">
          <el-card shadow="never" class="inner-card">
            <template #header>
              <div class="inner-header">角色权限配置</div>
            </template>

            <el-form label-position="top">
              <el-form-item label="选择角色">
                <el-select v-model="selectedRoleId" placeholder="请选择角色" style="width: 100%">
                  <el-option
                    v-for="r in roles"
                    :key="r.id"
                    :label="r.displayName || r.name"
                    :value="r.id"
                  >
                    <span style="float: left">{{ r.displayName || r.name }}</span>
                    <span style="float: right; color: var(--el-text-color-secondary); font-size: 13px">
                      {{ r.name }}
                    </span>
                  </el-option>
                </el-select>
              </el-form-item>

              <el-divider />

              <el-table :data="permissions" border stripe style="width: 100%; margin-bottom: 20px">
                <el-table-column prop="code" label="权限标识 (Code)" width="180" />
                <el-table-column prop="name" label="权限名称" />
                <el-table-column label="授权" width="100" align="center">
                  <template #default="{ row }">
                    <el-switch
                      :model-value="roleCodes.has(String(row.code || '').toLowerCase())"
                      @change="toggleRolePermission(row.code)"
                    />
                  </template>
                </el-table-column>
                <template #empty>
                  <el-empty description="暂无权限定义" />
                </template>
              </el-table>

              <div class="action-bar">
                <el-button
                  type="primary"
                  :disabled="busy || !selectedRoleId"
                  @click="saveRolePermissions"
                  :loading="busy"
                >
                  保存角色权限
                </el-button>
                <span class="hint-text">
                  <el-icon><InfoFilled /></el-icon>
                  需要后端具备 perm:settings.manage 权限
                </span>
              </div>
            </el-form>
          </el-card>
        </el-col>

        <!-- ================= 用户角色 ================= -->
        <el-col :span="10">
          <el-card shadow="never" class="inner-card">
            <template #header>
              <div class="inner-header">用户角色分配</div>
            </template>

            <el-form label-position="top">
              <el-form-item label="选择用户">
                <el-select
                  v-model="selectedUserId"
                  placeholder="请选择用户"
                  style="width: 100%"
                  @change="loadUserRole"
                >
                  <el-option
                    v-for="u in users"
                    :key="u.id"
                    :label="u.username"
                    :value="u.id"
                    :disabled="!u.isActive"
                  >
                    <span style="float: left">{{ u.username }}</span>
                    <span
                      v-if="!u.isActive"
                      style="float: right; color: var(--el-color-danger); font-size: 13px"
                    >
                      禁用
                    </span>
                  </el-option>
                </el-select>
              </el-form-item>

              <el-form-item label="分配角色">
                <el-select
                  v-model="selectedUserRoleName"
                  placeholder="请选择角色"
                  style="width: 100%"
                  clearable
                >
                  <el-option
                    v-for="r in roles"
                    :key="r.id"
                    :label="r.displayName || r.name"
                    :value="r.name"
                  />
                </el-select>
              </el-form-item>

              <div class="action-bar" style="margin-top: 40px">
                <el-button
                  type="primary"
                  :disabled="busy || !selectedUserId"
                  @click="saveUserRole"
                  :loading="busy"
                >
                  保存用户角色
                </el-button>
                <span class="hint-text">
                  <el-icon><InfoFilled /></el-icon>
                  需要后端具备 perm:settings.manage 权限
                </span>
              </div>
            </el-form>
          </el-card>
        </el-col>
      </el-row>
    </el-card>
  </div>
</template>
<style scoped>
.rbac-container {
  display: flex;
  flex-direction: column;
}

.main-card {
  border: none;
  border-radius: 8px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header-title {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.inner-card {
  height: 100%;
  border-radius: 6px;
  background-color: #fafafa;
  border: 1px solid #ebeef5;
}

.inner-header {
  font-size: 14px;
  font-weight: 600;
  color: #606266;
}

.action-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 20px;
}

.hint-text {
  font-size: 12px;
  color: #909399;
  display: flex;
  align-items: center;
  gap: 4px;
}
</style>
