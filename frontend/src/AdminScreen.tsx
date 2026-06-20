import { useEffect, useState, type FormEvent } from "react";
import {
  createAdminTicketCategory,
  getAdminRoles,
  getAdminSlaPolicies,
  getAdminTicketCategories,
  getAdminUsers,
  updateAdminSlaPolicy,
  updateAdminTicketCategory,
  updateAdminUser
} from "./api";
import { formatPriority, ticketPriorities } from "./ticketFormat";
import type {
  AdminUser,
  SlaPolicy,
  TicketCategory,
  UpdateAdminUserInput,
  UpsertSlaPolicyInput,
  UpsertTicketCategoryInput,
  UserProfile
} from "./types";

const emptyCategoryInput: UpsertTicketCategoryInput = {
  name: "",
  description: "",
  isActive: true
};

export function AdminScreen({ token, user }: { token: string; user: UserProfile }) {
  const canAdmin = user.roles.includes("Admin");
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [roles, setRoles] = useState<string[]>([]);
  const [categories, setCategories] = useState<TicketCategory[]>([]);
  const [slaPolicies, setSlaPolicies] = useState<SlaPolicy[]>([]);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [userInput, setUserInput] = useState<UpdateAdminUserInput>({ displayName: "", isActive: true, roles: [] });
  const [editingCategoryId, setEditingCategoryId] = useState<string | null>(null);
  const [categoryInput, setCategoryInput] = useState<UpsertTicketCategoryInput>(emptyCategoryInput);
  const [editingSlaId, setEditingSlaId] = useState<string | null>(null);
  const [slaInput, setSlaInput] = useState<UpsertSlaPolicyInput>({
    name: "",
    priority: 1,
    firstResponseMinutes: 1440,
    resolutionMinutes: 4320,
    isActive: true
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const activeUsers = users.filter((item) => item.isActive).length;
  const activeCategories = categories.filter((item) => item.isActive).length;
  const activeSlaPolicies = slaPolicies.filter((item) => item.isActive).length;

  useEffect(() => {
    if (!canAdmin) {
      setIsLoading(false);
      return;
    }

    loadAdminData();
  }, [canAdmin, token]);

  async function loadAdminData() {
    setIsLoading(true);
    setError(null);

    try {
      const [nextUsers, nextRoles, nextCategories, nextPolicies] = await Promise.all([
        getAdminUsers(token),
        getAdminRoles(token),
        getAdminTicketCategories(token),
        getAdminSlaPolicies(token)
      ]);
      setUsers(nextUsers);
      setRoles(nextRoles);
      setCategories(nextCategories);
      setSlaPolicies(nextPolicies);

      if (!selectedUserId && nextUsers[0]) {
        editUser(nextUsers[0]);
      }
      if (!editingSlaId && nextPolicies[0]) {
        editSlaPolicy(nextPolicies[0]);
      }
    } catch (err) {
      setError(readError(err, "Failed to load admin data."));
    } finally {
      setIsLoading(false);
    }
  }

  function editUser(nextUser: AdminUser) {
    setSelectedUserId(nextUser.id);
    setUserInput({
      displayName: nextUser.displayName,
      isActive: nextUser.isActive,
      roles: nextUser.roles
    });
  }

  async function saveUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedUserId) {
      return;
    }

    setIsSaving(true);
    setError(null);

    try {
      const updated = await updateAdminUser(token, selectedUserId, userInput);
      setUsers((current) => current.map((item) => (item.id === updated.id ? updated : item)));
      editUser(updated);
    } catch (err) {
      setError(readError(err, "Failed to save user."));
    } finally {
      setIsSaving(false);
    }
  }

  async function saveCategory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);

    try {
      if (editingCategoryId) {
        await updateAdminTicketCategory(token, editingCategoryId, categoryInput);
      } else {
        await createAdminTicketCategory(token, categoryInput);
      }

      setEditingCategoryId(null);
      setCategoryInput({ ...emptyCategoryInput });
      setCategories(await getAdminTicketCategories(token));
    } catch (err) {
      setError(readError(err, "Failed to save category."));
    } finally {
      setIsSaving(false);
    }
  }

  async function saveSla(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editingSlaId) {
      return;
    }

    setIsSaving(true);
    setError(null);

    try {
      const updated = await updateAdminSlaPolicy(token, editingSlaId, slaInput);
      setSlaPolicies((current) => current.map((item) => (item.id === updated.id ? updated : item)));
      editSlaPolicy(updated);
    } catch (err) {
      setError(readError(err, "Failed to save SLA policy."));
    } finally {
      setIsSaving(false);
    }
  }

  function editCategory(category: TicketCategory) {
    setEditingCategoryId(category.id);
    setCategoryInput({
      name: category.name,
      description: category.description ?? "",
      isActive: category.isActive
    });
  }

  function editSlaPolicy(policy: SlaPolicy) {
    setEditingSlaId(policy.id);
    setSlaInput({
      name: policy.name,
      priority: Number(policy.priority),
      firstResponseMinutes: policy.firstResponseMinutes,
      resolutionMinutes: policy.resolutionMinutes,
      isActive: policy.isActive
    });
  }

  function toggleRole(role: string) {
    setUserInput((current) => {
      const hasRole = current.roles.includes(role);
      return {
        ...current,
        roles: hasRole ? current.roles.filter((item) => item !== role) : [...current.roles, role]
      };
    });
  }

  if (!canAdmin) {
    return (
      <section className="admin-shell">
        <div className="notice notice-error">Admin role is required to manage users, categories and SLA policies.</div>
      </section>
    );
  }

  return (
    <section className="admin-shell">
      {error ? <div className="notice notice-error">{error}</div> : null}

      <header className="admin-hero">
        <div>
          <span className="eyebrow">Administration</span>
          <h2>Control users, routing and SLA contracts.</h2>
          <p>Manage operational access, ticket categories and policy thresholds from one protected surface.</p>
        </div>
        <button type="button" onClick={loadAdminData} disabled={isLoading}>
          Refresh
        </button>
      </header>

      <section className="admin-summary" aria-label="Administration summary">
        <SummaryCard label="Users" value={users.length} detail={`${activeUsers} active`} />
        <SummaryCard label="Categories" value={categories.length} detail={`${activeCategories} active`} />
        <SummaryCard label="SLA policies" value={slaPolicies.length} detail={`${activeSlaPolicies} active`} />
      </section>

      <div className="admin-grid">
        <section className="admin-panel admin-users-panel">
          <h3>Users and roles</h3>
          <div className="admin-user-workspace">
            <div className="admin-list admin-user-list">
              {users.map((item) => (
                <button
                  key={item.id}
                  className={item.id === selectedUserId ? "admin-row admin-row-active" : "admin-row"}
                  type="button"
                  onClick={() => editUser(item)}
                >
                  <span>
                    <strong>{item.displayName}</strong>
                    <small>{item.email}</small>
                  </span>
                  <em>{item.isActive ? "Active" : "Disabled"}</em>
                </button>
              ))}
            </div>

            <form className="admin-form admin-user-editor" onSubmit={saveUser}>
              <label>
                Display name
                <input
                  value={userInput.displayName}
                  onChange={(event) => setUserInput((current) => ({ ...current, displayName: event.target.value }))}
                  required
                />
              </label>
              <div className="admin-role-grid">
                {roles.map((role) => (
                  <label key={role} className="admin-check">
                    <input checked={userInput.roles.includes(role)} type="checkbox" onChange={() => toggleRole(role)} />
                    {role}
                  </label>
                ))}
              </div>
              <label className="admin-check">
                <input
                  checked={userInput.isActive}
                  type="checkbox"
                  onChange={(event) => setUserInput((current) => ({ ...current, isActive: event.target.checked }))}
                />
                Active account
              </label>
              <button type="submit" disabled={isSaving || !selectedUserId}>
                Save user
              </button>
            </form>
          </div>
        </section>

        <section className="admin-panel">
          <h3>Ticket categories</h3>
          <form className="admin-form" onSubmit={saveCategory}>
            <label>
              Name
              <input
                value={categoryInput.name}
                onChange={(event) => setCategoryInput((current) => ({ ...current, name: event.target.value }))}
                required
              />
            </label>
            <label>
              Description
              <textarea
                value={categoryInput.description ?? ""}
                onChange={(event) =>
                  setCategoryInput((current) => ({ ...current, description: event.target.value }))
                }
              />
            </label>
            <label className="admin-check">
              <input
                checked={categoryInput.isActive}
                type="checkbox"
                onChange={(event) => setCategoryInput((current) => ({ ...current, isActive: event.target.checked }))}
              />
              Available for new tickets
            </label>
            <button type="submit" disabled={isSaving}>
              {editingCategoryId ? "Update category" : "Create category"}
            </button>
          </form>
          <div className="admin-list">
            {categories.map((category) => (
              <button key={category.id} className="admin-row" type="button" onClick={() => editCategory(category)}>
                <span>
                  <strong>{category.name}</strong>
                  <small>{category.description ?? "No description"}</small>
                </span>
                <em>{category.isActive ? "Active" : "Off"}</em>
              </button>
            ))}
          </div>
        </section>

        <section className="admin-panel">
          <h3>SLA policies</h3>
          <form className="admin-form" onSubmit={saveSla}>
            <label>
              Name
              <input
                value={slaInput.name}
                onChange={(event) => setSlaInput((current) => ({ ...current, name: event.target.value }))}
                required
              />
            </label>
            <label>
              Priority
              <select
                value={slaInput.priority}
                onChange={(event) => setSlaInput((current) => ({ ...current, priority: Number(event.target.value) }))}
              >
                {ticketPriorities.map((priority) => (
                  <option key={priority.value} value={priority.value}>
                    {priority.label}
                  </option>
                ))}
              </select>
            </label>
            <div className="admin-two-col">
              <label>
                First response, min
                <input
                  min={1}
                  type="number"
                  value={slaInput.firstResponseMinutes}
                  onChange={(event) =>
                    setSlaInput((current) => ({ ...current, firstResponseMinutes: Number(event.target.value) }))
                  }
                  required
                />
              </label>
              <label>
                Resolution, min
                <input
                  min={1}
                  type="number"
                  value={slaInput.resolutionMinutes}
                  onChange={(event) =>
                    setSlaInput((current) => ({ ...current, resolutionMinutes: Number(event.target.value) }))
                  }
                  required
                />
              </label>
            </div>
            <label className="admin-check">
              <input
                checked={slaInput.isActive}
                type="checkbox"
                onChange={(event) => setSlaInput((current) => ({ ...current, isActive: event.target.checked }))}
              />
              Active policy
            </label>
            <button type="submit" disabled={isSaving || !editingSlaId}>
              Save SLA policy
            </button>
          </form>
          <div className="admin-list">
            {slaPolicies.map((policy) => (
              <button key={policy.id} className="admin-row" type="button" onClick={() => editSlaPolicy(policy)}>
                <span>
                  <strong>{policy.name || formatPriority(policy.priority)}</strong>
                  <small>
                    First response: {policy.firstResponseMinutes}m · Resolution: {policy.resolutionMinutes}m
                  </small>
                </span>
                <em>{policy.isActive ? "Active" : "Off"}</em>
              </button>
            ))}
          </div>
        </section>
      </div>
    </section>
  );
}

function SummaryCard({ label, value, detail }: { label: string; value: number; detail: string }) {
  return (
    <article className="admin-summary-card">
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </article>
  );
}

function readError(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}
