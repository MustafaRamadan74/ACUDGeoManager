'use client';

import React, { useState, useEffect } from 'react';
import { UserResponse, NetworkDto, AuditLogEntry } from '@/lib/types';
import { api } from '@/lib/api';
import { useToast } from '@/components/Toast';
import { useAuth } from '@/lib/authContext';
import {
  Users,
  UserPlus,
  X,
  Loader2,
  Shield,
  Activity,
  CheckCircle2,
  XCircle,
  Clock,
  Key,
  Layers,
  ChevronDown,
  ChevronRight,
  RefreshCw,
} from 'lucide-react';

interface UserAdminModalProps {
  networks: NetworkDto[];
  onClose: () => void;
}

export const UserAdminModal: React.FC<UserAdminModalProps> = ({ networks, onClose }) => {
  const { lang, user: currentUser } = useAuth();
  const { showSuccess, showError } = useToast();
  const isAr = lang === 'ar';

  const [users, setUsers] = useState<UserResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<'users' | 'create' | 'activity'>('users');
  const [selectedUserForActivity, setSelectedUserForActivity] = useState<UserResponse | null>(null);
  const [userActivity, setUserActivity] = useState<AuditLogEntry[]>([]);
  const [loadingActivity, setLoadingActivity] = useState(false);

  // Create User Form State
  const [newUsername, setNewUsername] = useState('');
  const [newEmail, setNewEmail] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [newRole, setNewRole] = useState<'ADMIN' | 'EDITOR' | 'VIEWER'>('EDITOR');
  const [selectedNetworkIds, setSelectedNetworkIds] = useState<string[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const fetchUsers = () => {
    setLoading(true);
    api
      .getUsers()
      .then((data) => setUsers(data))
      .catch((err) => {
        console.error('Failed to load users:', err);
        showError(isAr ? 'فشل تحميل قائمة المستخدمين' : 'Failed to load users', err.message);
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchUsers();
  }, []);

  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newUsername || !newEmail || !newPassword) {
      showError(isAr ? 'بيانات ناقصة' : 'Missing fields', isAr ? 'يرجى ملء جميع الحقول المطلوبة' : 'Please fill all fields');
      return;
    }

    setIsSubmitting(true);
    try {
      await api.createUser({
        username: newUsername.trim(),
        email: newEmail.trim(),
        password: newPassword,
        role: newRole,
        networkIds: selectedNetworkIds,
      });

      showSuccess(
        isAr ? 'تم إنشاء المستخدم بنجاح' : 'User created successfully',
        isAr ? `تمت إضافة المستخدم "${newUsername}" إلى النظام بنجاح.` : `User ${newUsername} added.`
      );

      // Reset form
      setNewUsername('');
      setNewEmail('');
      setNewPassword('');
      setNewRole('EDITOR');
      setSelectedNetworkIds([]);
      setActiveTab('users');
      fetchUsers();
    } catch (err: any) {
      console.error('Create user error:', err);
      showError(isAr ? 'فشل إنشاء المستخدم' : 'Failed to create user', err.message);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleToggleActive = async (targetUser: UserResponse) => {
    try {
      await api.updateUser(targetUser.id, {
        isActive: !targetUser.isActive,
      });
      showSuccess(
        isAr ? 'تم تعديل حالة المستخدم' : 'User status updated',
        `${targetUser.username}: ${!targetUser.isActive ? (isAr ? 'تم التفعيل' : 'Active') : (isAr ? 'تم التعطيل' : 'Inactive')}`
      );
      fetchUsers();
    } catch (err: any) {
      showError(isAr ? 'فشل تحديث الحالة' : 'Failed to update status', err.message);
    }
  };

  const handleChangeRole = async (targetUser: UserResponse, newRole: 'ADMIN' | 'EDITOR' | 'VIEWER') => {
    try {
      await api.updateUser(targetUser.id, {
        role: newRole,
      });
      showSuccess(
        isAr ? 'تم تغيير الصلاحية بنجاح' : 'Role updated successfully',
        `${targetUser.username} => ${newRole}`
      );
      fetchUsers();
    } catch (err: any) {
      showError(isAr ? 'فشل تغيير الصلاحية' : 'Failed to update role', err.message);
    }
  };

  const handleViewActivity = (targetUser: UserResponse) => {
    setSelectedUserForActivity(targetUser);
    setActiveTab('activity');
    setLoadingActivity(true);
    api
      .getUserActivity(targetUser.id)
      .then((res) => {
        setUserActivity(res.items || []);
      })
      .catch((err) => {
        console.error('Failed to load user activity:', err);
        showError(isAr ? 'فشل تحميل سجل النشاط' : 'Failed to load user activity', err.message);
      })
      .finally(() => setLoadingActivity(false));
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-700/80 rounded-2xl max-w-4xl w-full shadow-2xl flex flex-col max-h-[88vh] overflow-hidden">
        {/* Header */}
        <div className="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/80">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-lg bg-sky-500/10 border border-sky-500/30 text-sky-400">
              <Users className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <span>{isAr ? 'إدارة المستخدمين والصلاحيات (Admin Panel)' : 'User & Role Administration'}</span>
              </h3>
              <p className="text-[11px] text-slate-400">
                {isAr
                  ? 'إدارة حسابات المهندسين، الصلاحيات، نطاقات العمل وسجل النشاط'
                  : 'Manage engineer accounts, roles, workspace scopes, and activity.'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={fetchUsers}
              disabled={loading}
              className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
              title={isAr ? 'تحديث القائمة' : 'Refresh'}
            >
              <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            </button>
            <button
              onClick={onClose}
              className="p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Tab Navigation */}
        <div className="px-5 pt-3 border-b border-slate-800 bg-slate-950/40 flex items-center gap-4 text-xs font-semibold">
          <button
            onClick={() => setActiveTab('users')}
            className={`pb-2.5 border-b-2 flex items-center gap-2 transition ${
              activeTab === 'users'
                ? 'border-sky-500 text-sky-400'
                : 'border-transparent text-slate-400 hover:text-white'
            }`}
          >
            <Users className="w-4 h-4" />
            <span>{isAr ? 'قائمة المستخدمين' : 'All Users'} ({users.length})</span>
          </button>

          <button
            onClick={() => setActiveTab('create')}
            className={`pb-2.5 border-b-2 flex items-center gap-2 transition ${
              activeTab === 'create'
                ? 'border-sky-500 text-sky-400'
                : 'border-transparent text-slate-400 hover:text-white'
            }`}
          >
            <UserPlus className="w-4 h-4" />
            <span>{isAr ? 'إضافة مستخدم جديد' : 'Add New User'}</span>
          </button>

          {selectedUserForActivity && (
            <button
              onClick={() => setActiveTab('activity')}
              className={`pb-2.5 border-b-2 flex items-center gap-2 transition ${
                activeTab === 'activity'
                  ? 'border-sky-500 text-sky-400'
                  : 'border-transparent text-slate-400 hover:text-white'
              }`}
            >
              <Activity className="w-4 h-4" />
              <span>
                {isAr ? 'نشاط:' : 'Activity:'} {selectedUserForActivity.username}
              </span>
            </button>
          )}
        </div>

        {/* Content Body */}
        <div className="flex-1 overflow-y-auto p-5">
          {/* TAB 1: USERS LIST */}
          {activeTab === 'users' && (
            <div className="space-y-4">
              {loading ? (
                <div className="py-20 flex flex-col items-center justify-center text-slate-400 gap-3">
                  <Loader2 className="w-8 h-8 animate-spin text-sky-400" />
                  <span className="text-xs">{isAr ? 'جارٍ تحميل المستخدمين...' : 'Loading users...'}</span>
                </div>
              ) : (
                <div className="rounded-xl border border-slate-800 overflow-hidden bg-slate-950/40">
                  <table className="w-full text-xs text-left rtl:text-right">
                    <thead className="bg-slate-950 text-slate-400 border-b border-slate-800 font-semibold select-none">
                      <tr>
                        <th className="p-3">{isAr ? 'اسم المستخدم' : 'Username'}</th>
                        <th className="p-3">{isAr ? 'البريد الإلكتروني' : 'Email'}</th>
                        <th className="p-3">{isAr ? 'الدور / الصلاحية' : 'Role'}</th>
                        <th className="p-3 text-center">{isAr ? 'الحالة' : 'Status'}</th>
                        <th className="p-3">{isAr ? 'الشبكات المسموحة' : 'Networks'}</th>
                        <th className="p-3 text-center">{isAr ? 'الإجراءات' : 'Actions'}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-800/80 text-slate-300">
                      {users.map((u) => {
                        const isSelf = u.id === currentUser?.id;
                        return (
                          <tr key={u.id} className="hover:bg-slate-800/40 transition">
                            <td className="p-3 font-semibold text-white">
                              <div className="flex items-center gap-1.5">
                                <span>{u.username}</span>
                                {isSelf && (
                                  <span className="text-[10px] text-sky-400 bg-sky-950/80 px-1.5 py-0.2 rounded border border-sky-800">
                                    {isAr ? 'حسابك' : 'You'}
                                  </span>
                                )}
                              </div>
                            </td>
                            <td className="p-3 text-slate-400 font-mono text-[11px]">{u.email}</td>
                            <td className="p-3">
                              <select
                                value={u.role}
                                onChange={(e) => handleChangeRole(u, e.target.value as any)}
                                disabled={isSelf}
                                className="bg-slate-900 border border-slate-700 rounded-lg px-2 py-1 text-xs text-white focus:outline-none focus:border-sky-500 font-semibold disabled:opacity-50"
                              >
                                <option value="ADMIN">ADMIN</option>
                                <option value="EDITOR">EDITOR</option>
                                <option value="VIEWER">VIEWER</option>
                              </select>
                            </td>
                            <td className="p-3 text-center">
                              <button
                                onClick={() => handleToggleActive(u)}
                                disabled={isSelf}
                                className={`px-2.5 py-1 rounded-full text-[10px] font-bold transition flex items-center justify-center gap-1 mx-auto disabled:opacity-50 ${
                                  u.isActive
                                    ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 hover:bg-emerald-500/30'
                                    : 'bg-rose-500/20 text-rose-300 border border-rose-500/40 hover:bg-rose-500/30'
                                }`}
                              >
                                {u.isActive ? (
                                  <>
                                    <CheckCircle2 className="w-3 h-3" />
                                    <span>{isAr ? 'مفعل' : 'Active'}</span>
                                  </>
                                ) : (
                                  <>
                                    <XCircle className="w-3 h-3" />
                                    <span>{isAr ? 'معطل' : 'Disabled'}</span>
                                  </>
                                )}
                              </button>
                            </td>
                            <td className="p-3 text-[11px] text-slate-400">
                              {u.accessibleNetworkIds && u.accessibleNetworkIds.length > 0 ? (
                                <div className="flex gap-1 flex-wrap">
                                  {u.accessibleNetworkIds.map((netId) => {
                                    const net = networks.find((n) => n.id === netId);
                                    return (
                                      <span
                                        key={netId}
                                        className="px-1.5 py-0.5 rounded bg-slate-800 text-slate-300 font-mono text-[10px]"
                                      >
                                        {net?.domain || 'NET'}
                                      </span>
                                    );
                                  })}
                                </div>
                              ) : (
                                <span className="text-slate-500 italic">{isAr ? 'الكل' : 'All'}</span>
                              )}
                            </td>
                            <td className="p-3 text-center">
                              <button
                                onClick={() => handleViewActivity(u)}
                                className="px-2.5 py-1 rounded-lg bg-sky-600/20 hover:bg-sky-600/30 text-sky-300 text-xs font-medium transition border border-sky-500/30 flex items-center gap-1 mx-auto"
                              >
                                <Activity className="w-3 h-3" />
                                <span>{isAr ? 'النشاط' : 'Activity'}</span>
                              </button>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

          {/* TAB 2: CREATE USER */}
          {activeTab === 'create' && (
            <form onSubmit={handleCreateUser} className="max-w-lg mx-auto space-y-4">
              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-300">
                  {isAr ? 'اسم المستخدم (Username)' : 'Username'} <span className="text-rose-400">*</span>
                </label>
                <input
                  type="text"
                  value={newUsername}
                  onChange={(e) => setNewUsername(e.target.value)}
                  placeholder="e.g. ahmed_engineer"
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-sky-500"
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-300">
                  {isAr ? 'البريد الإلكتروني (Email)' : 'Email'} <span className="text-rose-400">*</span>
                </label>
                <input
                  type="email"
                  value={newEmail}
                  onChange={(e) => setNewEmail(e.target.value)}
                  placeholder="engineer@acud.gov.eg"
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-sky-500"
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-300">
                  {isAr ? 'كلمة المرور (Password)' : 'Password'} <span className="text-rose-400">*</span>
                </label>
                <input
                  type="password"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  placeholder="At least 8 characters"
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-sky-500"
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-300">
                  {isAr ? 'الدور والصلاحية (Role)' : 'Role'}
                </label>
                <select
                  value={newRole}
                  onChange={(e) => setNewRole(e.target.value as any)}
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-sky-500"
                >
                  <option value="VIEWER">{isAr ? 'مشاهد (VIEWER) - قراءة فقط' : 'VIEWER (Read-only)'}</option>
                  <option value="EDITOR">{isAr ? 'محرر (EDITOR) - رسم وتعديل وحذف' : 'EDITOR (Draw, edit, delete)'}</option>
                  <option value="ADMIN">{isAr ? 'مدير نظام (ADMIN) - صلاحيات كاملة' : 'ADMIN (Full privileges)'}</option>
                </select>
              </div>

              {/* Workspace / Networks selection */}
              <div className="space-y-2 pt-2">
                <label className="text-xs font-semibold text-slate-300 flex items-center gap-1.5">
                  <Layers className="w-3.5 h-3.5 text-sky-400" />
                  <span>{isAr ? 'نطاقات العمل المسموحة (Workspaces / Networks)' : 'Accessible Networks'}</span>
                </label>
                <div className="bg-slate-950/60 p-3 rounded-xl border border-slate-800 space-y-2">
                  {networks.map((net) => {
                    const checked = selectedNetworkIds.includes(net.id);
                    return (
                      <label key={net.id} className="flex items-center gap-2.5 text-xs text-slate-300 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={(e) => {
                            if (e.target.checked) {
                              setSelectedNetworkIds((prev) => [...prev, net.id]);
                            } else {
                              setSelectedNetworkIds((prev) => prev.filter((id) => id !== net.id));
                            }
                          }}
                          className="w-4 h-4 rounded text-sky-600 bg-slate-900 border-slate-700 focus:ring-0 cursor-pointer"
                        />
                        <span>{net.name}</span>
                        <span className="font-mono text-slate-500 text-[10px]">({net.domain})</span>
                      </label>
                    );
                  })}
                </div>
              </div>

              <div className="pt-3 flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setActiveTab('users')}
                  className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 transition"
                >
                  {isAr ? 'إلغاء' : 'Cancel'}
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="px-5 py-2 rounded-xl text-xs font-semibold text-white bg-sky-600 hover:bg-sky-500 disabled:opacity-50 transition flex items-center gap-2 shadow-lg shadow-sky-950/50"
                >
                  {isSubmitting ? (
                    <>
                      <Loader2 className="w-4 h-4 animate-spin" />
                      <span>{isAr ? 'جارٍ الإنشاء...' : 'Creating...'}</span>
                    </>
                  ) : (
                    <>
                      <UserPlus className="w-4 h-4" />
                      <span>{isAr ? 'إنشاء المستخدم' : 'Create User'}</span>
                    </>
                  )}
                </button>
              </div>
            </form>
          )}

          {/* TAB 3: USER ACTIVITY */}
          {activeTab === 'activity' && selectedUserForActivity && (
            <div className="space-y-4">
              <div className="p-3 rounded-xl bg-slate-950/60 border border-slate-800 flex items-center justify-between">
                <div className="flex items-center gap-2 text-xs">
                  <span className="text-slate-400">{isAr ? 'سجل العمليات المنفذة بواسطة:' : 'Operations by:'}</span>
                  <strong className="text-sky-400 font-mono text-sm">{selectedUserForActivity.username}</strong>
                  <span className="text-[10px] text-slate-500 uppercase px-1 rounded bg-slate-800">
                    {selectedUserForActivity.role}
                  </span>
                </div>
                <span className="text-xs text-slate-400 font-mono">
                  {userActivity.length} {isAr ? 'عملية مسجلة' : 'actions'}
                </span>
              </div>

              {loadingActivity ? (
                <div className="py-20 flex flex-col items-center justify-center text-slate-400 gap-3">
                  <Loader2 className="w-8 h-8 animate-spin text-sky-400" />
                  <span className="text-xs">{isAr ? 'جارٍ جلب سجل النشاط...' : 'Loading activity logs...'}</span>
                </div>
              ) : userActivity.length === 0 ? (
                <div className="py-16 text-center text-slate-500 text-xs italic">
                  {isAr ? 'لا توجد أنشطة مسجلة لهذا المستخدم حتى الآن.' : 'No audit activity recorded for this user.'}
                </div>
              ) : (
                <div className="space-y-2">
                  {userActivity.map((act) => (
                    <div
                      key={act.id}
                      className="p-3 rounded-xl border border-slate-800 bg-slate-950/50 flex items-center justify-between text-xs"
                    >
                      <div className="flex items-center gap-3">
                        <span
                          className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                            act.action === 'INSERT'
                              ? 'bg-emerald-500/20 text-emerald-300'
                              : act.action === 'UPDATE'
                              ? 'bg-sky-500/20 text-sky-300'
                              : 'bg-rose-500/20 text-rose-300'
                          }`}
                        >
                          {act.action}
                        </span>
                        <div>
                          <span className="font-semibold text-white">{act.tableName}</span>
                          <span className="text-[11px] text-slate-500 font-mono ml-2 rtl:ml-0 rtl:mr-2">
                            ID: {act.recordId}
                          </span>
                        </div>
                      </div>

                      <div className="flex items-center gap-2 text-[11px] text-slate-400">
                        <Clock className="w-3 h-3 text-slate-500" />
                        <span>{new Date(act.changedAt).toLocaleString(isAr ? 'ar-EG' : 'en-US')}</span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="p-3 border-t border-slate-800 bg-slate-950/60 flex justify-end">
          <button
            onClick={onClose}
            className="px-4 py-1.5 rounded-xl text-xs font-semibold text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 transition"
          >
            {isAr ? 'إغلاق' : 'Close'}
          </button>
        </div>
      </div>
    </div>
  );
};
