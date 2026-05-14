<script setup>
import { ref, onMounted } from "vue";
import { ElMessage, ElMessageBox } from "element-plus";
import { Plus, Edit, Delete, Search } from "@element-plus/icons-vue";
import {
  adminUsers,
  adminCreateUser,
  adminUpdateUser,
  adminDeleteUser,
  adminResetPassword,
} from "../api/users";
import { rbacRoles } from "../api/rbac";

const busy = ref(false);
const users = ref([]);
const roles = ref([]);
const searchText = ref("");

const dialogVisible = ref(false);
const dialogMode = ref("create");
const dialogTitle = ref("");
const form = ref({ id: 0, username: "", password: "", role: "", isActive: true });
const saving = ref(false);

const passwordDialogVisible = ref(false);
const passwordForm = ref({ userId: 0, username: "", password: "" });
const passwordSaving = ref(false);

const filteredUsers = () => {
  if (!searchText.value) return users.value;
  const q = searchText.value.toLowerCase();
  return users.value.filter(
    (u) =>
      u.username.toLowerCase().includes(q)
  );
};

async function load() {
  busy.value = true;
  try {
    users.value = await adminUsers(500);
    roles.value = await rbacRoles();
  } catch (e) {
    ElMessage.error("加载用户列表失败: " + (e?.message || e));
  } finally {
    busy.value = false;
  }
}

function showCreate() {
  dialogMode.value = "create";
  dialogTitle.value = "新建用户";
  form.value = { id: 0, username: "", password: "", role: roles.value[0]?.name || "user", isActive: true };
  dialogVisible.value = true;
}

function showEdit(row) {
  dialogMode.value = "edit";
  dialogTitle.value = `编辑用户: ${row.username}`;
  form.value = { id: row.id, username: row.username, password: "", role: "", isActive: row.isActive };
  dialogVisible.value = true;
}

async function save() {
  if (!form.value.username.trim() || form.value.username.trim().length < 3) {
    ElMessage.warning("用户名至少3个字符");
    return;
  }
  if (dialogMode.value === "create" && (!form.value.password || form.value.password.length < 6)) {
    ElMessage.warning("密码至少6个字符");
    return;
  }

  saving.value = true;
  try {
    if (dialogMode.value === "create") {
      await adminCreateUser({
        username: form.value.username.trim(),
        password: form.value.password,
        role: form.value.role || "user",
      });
      ElMessage.success("用户创建成功");
    } else {
      await adminUpdateUser(form.value.id, {
        username: form.value.username.trim(),
        isActive: form.value.isActive,
      });
      ElMessage.success("用户信息已更新");
    }
    dialogVisible.value = false;
    await load();
  } catch (e) {
    ElMessage.error("操作失败: " + (e?.message || e));
  } finally {
    saving.value = false;
  }
}

async function remove(row) {
  try {
    await ElMessageBox.confirm(
      `确定要删除用户 "${row.username}" 吗？此操作为软删除。`,
      "确认删除",
      { confirmButtonText: "删除", cancelButtonText: "取消", type: "warning" }
    );
  } catch {
    return;
  }

  busy.value = true;
  try {
    await adminDeleteUser(row.id);
    ElMessage.success("用户已删除");
    await load();
  } catch (e) {
    ElMessage.error("删除失败: " + (e?.message || e));
  } finally {
    busy.value = false;
  }
}

function showResetPassword(row) {
  passwordForm.value = { userId: row.id, username: row.username, password: "" };
  passwordDialogVisible.value = true;
}

async function doResetPassword() {
  if (!passwordForm.value.password || passwordForm.value.password.length < 6) {
    ElMessage.warning("密码至少6个字符");
    return;
  }
  passwordSaving.value = true;
  try {
    await adminResetPassword(passwordForm.value.userId, passwordForm.value.password);
    ElMessage.success(`用户 "${passwordForm.value.username}" 的密码已重置`);
    passwordDialogVisible.value = false;
  } catch (e) {
    ElMessage.error("密码重置失败: " + (e?.message || e));
  } finally {
    passwordSaving.value = false;
  }
}

function statusTag(isActive) {
  return isActive ? { type: "success", text: "启用" } : { type: "danger", text: "禁用" };
}

onMounted(load);
</script>

<template>
  <div class="users-container">
    <el-card class="main-card">
      <template #header>
        <div class="card-header">
          <span class="header-title">用户管理</span>
          <div class="header-actions">
            <el-input
              v-model="searchText"
              placeholder="搜索用户名..."
              clearable
              :prefix-icon="Search"
              style="width: 220px; margin-right: 12px"
            />
            <el-button type="primary" :icon="Plus" @click="showCreate" :disabled="busy">
              新建用户
            </el-button>
            <el-button :icon="'Refresh'" :loading="busy" @click="load">刷新</el-button>
          </div>
        </div>
      </template>

      <el-table :data="filteredUsers()" border stripe v-loading="busy" style="width: 100%">
        <el-table-column prop="id" label="ID" width="70" />
        <el-table-column prop="username" label="用户名" min-width="140" />
        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="statusTag(row.isActive).type" size="small">
              {{ statusTag(row.isActive).text }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="创建时间" width="170">
          <template #default="{ row }">
            {{ row.createdAt ? new Date(row.createdAt).toLocaleString() : '-' }}
          </template>
        </el-table-column>
        <el-table-column label="最后登录" width="170">
          <template #default="{ row }">
            {{ row.lastLoginAt ? new Date(row.lastLoginAt).toLocaleString() : '从未登录' }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="250" fixed="right">
          <template #default="{ row }">
            <el-button size="small" :icon="Edit" @click="showEdit(row)">编辑</el-button>
            <el-button size="small" type="warning" @click="showResetPassword(row)">改密</el-button>
            <el-button size="small" type="danger" :icon="Delete" @click="remove(row)">删除</el-button>
          </template>
        </el-table-column>
        <template #empty>
          <el-empty description="暂无用户数据" />
        </template>
      </el-table>
    </el-card>

    <!-- 新建/编辑对话框 -->
    <el-dialog v-model="dialogVisible" :title="dialogTitle" width="480px" :close-on-click-modal="false">
      <el-form :model="form" label-width="80px">
        <el-form-item label="用户名">
          <el-input v-model="form.username" placeholder="请输入用户名" maxlength="50" />
        </el-form-item>
        <el-form-item v-if="dialogMode === 'create'" label="密码">
          <el-input
            v-model="form.password"
            type="password"
            placeholder="至少6个字符"
            show-password
          />
        </el-form-item>
        <el-form-item v-if="dialogMode === 'create'" label="角色">
          <el-select v-model="form.role" placeholder="选择角色" style="width: 100%">
            <el-option
              v-for="r in roles"
              :key="r.id"
              :label="r.displayName || r.name"
              :value="r.name"
            />
          </el-select>
        </el-form-item>
        <el-form-item v-if="dialogMode === 'edit'" label="状态">
          <el-switch v-model="form.isActive" active-text="启用" inactive-text="禁用" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">
          {{ dialogMode === 'create' ? '创建' : '保存' }}
        </el-button>
      </template>
    </el-dialog>

    <!-- 改密对话框 -->
    <el-dialog v-model="passwordDialogVisible" title="重置密码" width="400px" :close-on-click-modal="false">
      <el-form :model="passwordForm" label-width="80px">
        <el-form-item label="用户">
          <span>{{ passwordForm.username }}</span>
        </el-form-item>
        <el-form-item label="新密码">
          <el-input
            v-model="passwordForm.password"
            type="password"
            placeholder="至少6个字符"
            show-password
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="passwordDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="passwordSaving" @click="doResetPassword">确认重置</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.users-container {
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

.header-actions {
  display: flex;
  align-items: center;
}
</style>
