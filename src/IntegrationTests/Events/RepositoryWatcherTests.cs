﻿using System;
using System.Linq;
using System.Threading;
using FluentAssertions;
using GitHub.Unity;
using NSubstitute;
using NUnit.Framework;

namespace IntegrationTests.Events
{
    class RepositoryWatcherTests : BaseGitIntegrationTest
    {
        protected override void OnSetup()
        {
            base.OnSetup();

            RepositoryManager = Substitute.For<IRepositoryManager>();

            DotGitPath = TestRepoPath.Combine(".git");

            if (DotGitPath.FileExists())
            {
                DotGitPath =
                    DotGitPath.ReadAllLines()
                              .Where(x => x.StartsWith("gitdir:"))
                              .Select(x => x.Substring(7).Trim())
                              .First();
            }

            BranchesPath = DotGitPath.Combine("refs", "heads");
            RemotesPath = DotGitPath.Combine("refs", "remotes");
            DotGitIndex = DotGitPath.Combine("index");
            DotGitHead = DotGitPath.Combine("HEAD");
            DotGitConfig = DotGitPath.Combine("config");
        }

        private RepositoryWatcher CreateRepositoryWatcher()
        {
            return new RepositoryWatcher(Platform, TestRepoPath, DotGitPath, DotGitIndex,
                DotGitHead, BranchesPath, RemotesPath, DotGitConfig);
        }

        protected IRepositoryManager RepositoryManager { get; private set; }

        protected NPath DotGitConfig { get; private set; }

        protected NPath DotGitHead { get; private set; }

        protected NPath DotGitIndex { get; private set; }

        protected NPath RemotesPath { get; private set; }

        protected NPath BranchesPath { get; private set; }

        protected NPath DotGitPath { get; private set; }

        [Test]
        public void ShouldDetectFileChanges()
        {
            var repositoryWatcher = CreateRepositoryWatcher();

            var repositoryWatcherListener = Substitute.For<IRepositoryWatcherListener>();
            repositoryWatcherListener.AttachListener(repositoryWatcher);

            repositoryWatcher.Start();

            try
            {
                var foobarTxt = TestRepoPath.Combine("foobar.txt");
                foobarTxt.WriteAllText("foobar");

                Thread.Sleep(500);

                repositoryWatcherListener.DidNotReceive().ConfigChanged();
                repositoryWatcherListener.DidNotReceive().HeadChanged(Args.String);
                repositoryWatcherListener.DidNotReceive().IndexChanged();
                repositoryWatcherListener.DidNotReceive().LocalBranchCreated(Args.String);
                repositoryWatcherListener.DidNotReceive().LocalBranchDeleted(Args.String);
                repositoryWatcherListener.DidNotReceive().LocalBranchChanged(Args.String);
                repositoryWatcherListener.DidNotReceive().RemoteBranchChanged(Args.String, Args.String);
                repositoryWatcherListener.DidNotReceive().RemoteBranchCreated(Args.String, Args.String);
                repositoryWatcherListener.DidNotReceive().RemoteBranchDeleted(Args.String, Args.String);
                repositoryWatcherListener.Received().RepositoryChanged();
            }
            finally
            {
                repositoryWatcher.Stop();
            }
        }

        [Test]
        public void ShouldDetectBranchChange()
        {
            var repositoryWatcher = CreateRepositoryWatcher();

            var repositoryWatcherListener = Substitute.For<IRepositoryWatcherListener>();
            repositoryWatcherListener.AttachListener(repositoryWatcher);

            repositoryWatcher.Start();

            try
            {
                SwitchBranch("feature/document");

                Thread.Sleep(500);

                repositoryWatcherListener.DidNotReceive().ConfigChanged();
                repositoryWatcherListener.Received(1).HeadChanged("ref: refs/heads/feature/document");
                repositoryWatcherListener.Received(1).IndexChanged();
                repositoryWatcherListener.DidNotReceive().LocalBranchCreated(Args.String);
                repositoryWatcherListener.DidNotReceive().LocalBranchDeleted(Args.String);
                repositoryWatcherListener.DidNotReceive().LocalBranchChanged(Args.String);
                repositoryWatcherListener.DidNotReceive().RemoteBranchChanged(Args.String, Args.String);
                repositoryWatcherListener.DidNotReceive().RemoteBranchCreated(Args.String, Args.String);
                repositoryWatcherListener.DidNotReceive().RemoteBranchDeleted(Args.String, Args.String);
                repositoryWatcherListener.Received().RepositoryChanged();
            }
            finally
            {
                repositoryWatcher.Stop();
            }
        }

        [Test]
        public void ShouldDetectChangesToRemotes()
        {
            var repositoryWatcher = CreateRepositoryWatcher();

            var repositoryWatcherListener = Substitute.For<IRepositoryWatcherListener>();
            repositoryWatcherListener.AttachListener(repositoryWatcher);

            repositoryWatcher.Start();

            try
            {
                GitRemoteRemove("origin");

                Thread.Sleep(500);

                repositoryWatcherListener.Received(1).ConfigChanged();
                repositoryWatcherListener.DidNotReceive().HeadChanged(Args.String);
                repositoryWatcherListener.DidNotReceive().IndexChanged();
                repositoryWatcherListener.DidNotReceive().LocalBranchCreated(Args.String);
                repositoryWatcherListener.DidNotReceive().LocalBranchDeleted(Args.String);
                repositoryWatcherListener.DidNotReceive().LocalBranchChanged(Args.String);
                repositoryWatcherListener.Received(1).RemoteBranchDeleted("origin", "feature/document");
                repositoryWatcherListener.Received(1).RemoteBranchDeleted("origin", "master");
                repositoryWatcherListener.DidNotReceive().RepositoryChanged();

                repositoryWatcherListener.ClearReceivedCalls();

                GitRemoteAdd("origin", "https://github.com/EvilStanleyGoldman/IOTestsRepo.git");

                Thread.Sleep(500);

                repositoryWatcherListener.Received(2).ConfigChanged();
                repositoryWatcherListener.DidNotReceive().HeadChanged(Args.String);
                repositoryWatcherListener.DidNotReceive().IndexChanged();
                repositoryWatcherListener.DidNotReceive().LocalBranchCreated(Args.String);
                repositoryWatcherListener.DidNotReceive().LocalBranchDeleted(Args.String);
                repositoryWatcherListener.DidNotReceive().LocalBranchChanged(Args.String);
                repositoryWatcherListener.DidNotReceive().RemoteBranchChanged(Args.String, Args.String);
                repositoryWatcherListener.DidNotReceive().RemoteBranchCreated(Args.String, Args.String);
                repositoryWatcherListener.DidNotReceive().RemoteBranchDeleted(Args.String, Args.String);
                repositoryWatcherListener.DidNotReceive().RepositoryChanged();
            }
            finally
            {
                repositoryWatcher.Stop();
            }
        }

        [Test]
        public void ShouldDetectGitPull()
        {
            var repositoryWatcher = CreateRepositoryWatcher();

            var repositoryWatcherListener = Substitute.For<IRepositoryWatcherListener>();
            repositoryWatcherListener.AttachListener(repositoryWatcher);

            repositoryWatcher.Start();
            try
            {
                //TODO: This is not expected
                new Action(() => { GitPull("origin", "master"); }).ShouldThrow<Exception>();

                repositoryWatcherListener.AssertDidNotReceiveAnyCalls();
            }
            finally
            {
                repositoryWatcher.Stop();
            }
        }

        private void SwitchBranch(string branch)
        {
            var completed = false;
            var gitSwitchBranchesTask = new GitSwitchBranchesTask(Environment, ProcessManager,
                new TaskResultDispatcher<string>(s => { completed = true; }), branch);

            gitSwitchBranchesTask.RunAsync(CancellationToken.None).Wait();
            Thread.Sleep(100);

            completed.Should().BeTrue();
        }
        private void GitPull(string remote, string branch)
        {
            var completed = false;

            var credentialManager = new GitCredentialManager(Environment, ProcessManager);
            var gitPullTask = new GitPullTask(Environment, ProcessManager, new TaskResultDispatcher<string>(s => { completed = true; }),
                credentialManager, new TestUIDispatcher(), remote, branch);

            gitPullTask.RunAsync(CancellationToken.None).Wait();
            Thread.Sleep(100);

            completed.Should().BeTrue();
        }

        private void GitRemoteAdd(string remote, string url)
        {
            var completed = false;

            var gitRemoteAddTask = new GitRemoteAddTask(Environment, ProcessManager,
                new TaskResultDispatcher<string>(s => { completed = true; }), remote, url);

            gitRemoteAddTask.RunAsync(CancellationToken.None).Wait();
            Thread.Sleep(100);

            completed.Should().BeTrue();
        }

        private void GitRemoteRemove(string remote)
        {
            var completed = false;

            var gitRemoteAddTask = new GitRemoteRemoveTask(Environment, ProcessManager,
                new TaskResultDispatcher<string>(s => { completed = true; }), remote);

            gitRemoteAddTask.RunAsync(CancellationToken.None).Wait();
            Thread.Sleep(100);

            completed.Should().BeTrue();
        }
    }

    public interface IRepositoryWatcherListener
    {
        void ConfigChanged();
        void HeadChanged(string obj);
        void IndexChanged();
        void LocalBranchCreated(string branch);
        void LocalBranchDeleted(string branch);
        void LocalBranchChanged(string branch);
        void RemoteBranchChanged(string remote, string branch);
        void RemoteBranchCreated(string remote, string branch);
        void RemoteBranchDeleted(string remote, string branch);
        void RepositoryChanged();
    }

    static class RepositoryWatcherListenerExtensions
    {
        public static void AttachListener(this IRepositoryWatcherListener listener, IRepositoryWatcher repositoryWatcher)
        {
            repositoryWatcher.ConfigChanged += listener.ConfigChanged;
            repositoryWatcher.HeadChanged += listener.HeadChanged;
            repositoryWatcher.IndexChanged += listener.IndexChanged;
            repositoryWatcher.LocalBranchCreated += listener.LocalBranchCreated;
            repositoryWatcher.LocalBranchDeleted += listener.LocalBranchDeleted;
            repositoryWatcher.RemoteBranchCreated += listener.RemoteBranchCreated;
            repositoryWatcher.RemoteBranchDeleted += listener.RemoteBranchDeleted;
            repositoryWatcher.RepositoryChanged += listener.RepositoryChanged;
        }

        public static void AssertDidNotReceiveAnyCalls(this IRepositoryWatcherListener repositoryWatcherListener)
        {
            repositoryWatcherListener.DidNotReceive().ConfigChanged();
            repositoryWatcherListener.DidNotReceive().HeadChanged(Args.String);
            repositoryWatcherListener.DidNotReceive().IndexChanged();
            repositoryWatcherListener.DidNotReceive().LocalBranchCreated(Args.String);
            repositoryWatcherListener.DidNotReceive().LocalBranchDeleted(Args.String);
            repositoryWatcherListener.DidNotReceive().LocalBranchChanged(Args.String);
            repositoryWatcherListener.DidNotReceive().RemoteBranchChanged(Args.String, Args.String);
            repositoryWatcherListener.DidNotReceive().RemoteBranchCreated(Args.String, Args.String);
            repositoryWatcherListener.DidNotReceive().RemoteBranchDeleted(Args.String, Args.String);
            repositoryWatcherListener.DidNotReceive().RepositoryChanged();
        }
    }
}
